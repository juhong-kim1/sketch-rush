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

    public event Action<string> OnRecognized; // 인식 완료 이벤트
    public event Action<string> OnError; // 에러 이벤트

    private string apiKey;
    private APIConfig config;

    private void Awake()
    {
        config = Resources.Load<APIConfig>("APIConfig");

        apiKey = config.GoogleSpeechApiKey;
    }

    // 녹음 시작
    public void StartRecording()
    {
        if (isRecording) return;

        Debug.Log("[VoiceRecognizer] Recording started");
        recordedClip = Microphone.Start(null, false, 10, 16000);
        isRecording = true;
    }

    // 녹음 정지 및 인식
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

        // 1. AudioClip → WAV 변환
        byte[] wavData = ConvertToWAV(recordedClip);

        // 2. Base64 인코딩
        string base64Audio = Convert.ToBase64String(wavData);

        // 3. Google API 요청 JSON
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

        // 4. HTTP 요청
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

            // 5. 결과 파싱
            string result = ParseResponse(request.downloadHandler.text);

            if (!string.IsNullOrEmpty(result))
            {
                Debug.Log($"[VoiceRecognizer] Recognized: {result}");
                OnRecognized?.Invoke(result);
            }
            else
            {
                Debug.LogWarning("[VoiceRecognizer] No speech detected");
                OnError?.Invoke("음성을 인식하지 못했습니다");
            }
        }
        else
        {
            Debug.LogError($"[VoiceRecognizer] Error: {request.error}");
            OnError?.Invoke("음성 인식 실패");
        }

        IsProcessing = false;
    }

    // WAV 변환
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

    // 응답 파싱
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