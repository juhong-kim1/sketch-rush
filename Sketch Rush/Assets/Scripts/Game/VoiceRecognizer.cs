using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public class VoiceRecognizer : MonoBehaviour
{
    private AudioClip recordedClip;
    private bool isRecording = false;
    public bool IsProcessing { get; private set; } = false;

    public event Action<string> OnRecognized; // �ν� �Ϸ� �̺�Ʈ
    public event Action<string> OnError; // ���� �̺�Ʈ

    private string apiKey;
    private APIConfig config;

    private void Awake()
    {
        config = Resources.Load<APIConfig>("APIConfig");

        apiKey = config.GoogleSpeechApiKey;
    }

    IEnumerator Start()
    {
#if UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            yield return new WaitForSeconds(1f);
        }
#elif UNITY_IOS
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
#else
        yield return null;
#endif
    }

    // ���� ����
    public void StartRecording()
    {
        if (isRecording) return;

        Debug.Log("[VoiceRecognizer] Recording started");
        recordedClip = Microphone.Start(null, false, 10, 16000);
        isRecording = true;
    }

    // ���� ���� �� �ν�
    public void StopRecordingAndRecognize()
    {
        if (!isRecording) return;

        Debug.Log("[VoiceRecognizer] Recording stopped");
        Microphone.End(null);
        isRecording = false;

        IsProcessing = true;
        StartCoroutine(SendToGoogle());
    }

    IEnumerator SendToGoogle()
    {
        Debug.Log("[VoiceRecognizer] Converting audio...");

        // 1. AudioClip �� WAV ��ȯ
        byte[] wavData = ConvertToWAV(recordedClip);

        // 2. Base64 ���ڵ�
        string base64Audio = Convert.ToBase64String(wavData);

        // 3. Google API ��û JSON
        string json = $@"{{
            ""config"": {{
                ""encoding"": ""LINEAR16"",
                ""sampleRateHertz"": 16000,
                ""languageCode"": ""ko-KR""
            }},
            ""audio"": {{
                ""content"": ""{base64Audio}""
            }}
        }}";

        Debug.Log("[VoiceRecognizer] Sending to Google...");

        // 4. HTTP ��û
        string url = $"https://speech.googleapis.com/v1/speech:recognize?key={apiKey}";

        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[VoiceRecognizer] Response: " + request.downloadHandler.text);

            // 5. ��� �Ľ�
            string result = ParseResponse(request.downloadHandler.text);

            if (!string.IsNullOrEmpty(result))
            {
                Debug.Log($"[VoiceRecognizer] Recognized: {result}");
                OnRecognized?.Invoke(result);
            }
            else
            {
                Debug.LogWarning("[VoiceRecognizer] No speech detected");
                OnError?.Invoke("������ �ν����� ���߽��ϴ�");
            }
        }
        else
        {
            Debug.LogError($"[VoiceRecognizer] Error: {request.error}");
            OnError?.Invoke("���� �ν� ����");
        }

        IsProcessing = false;
    }

    // WAV ��ȯ
    byte[] ConvertToWAV(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];

        int rescaleFactor = 32767;

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * rescaleFactor);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        return bytesData;
    }

    // ���� �Ľ�
    string ParseResponse(string json)
    {
        try
        {
            Match match = Regex.Match(json, "\"transcript\":\\s*\"([^\"]+)\"");

            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[VoiceRecognizer] Parse error: {e.Message}");
        }

        return null;
    }
}