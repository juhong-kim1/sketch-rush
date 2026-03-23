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

    public event Action<string> OnRecognized;
    public event Action<string> OnError;

    private const string FUNCTIONS_URL = "https://us-central1-sketch-rush-4f559.cloudfunctions.net/recognizeSpeech";

    IEnumerator Start()
    {
#if UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
            yield return new WaitUntil(() =>
                UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone));
        }
        else
        {
            yield return null;
        }
#elif UNITY_IOS
        yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
#else
        yield return null;
#endif
    }

    public void StartRecording()
    {
        if (isRecording) return;

        Debug.Log("[VoiceRecognizer] Recording started");
        recordedClip = Microphone.Start(null, false, 10, 16000);
        isRecording = true;
    }

    public void StopRecordingAndRecognize()
    {
        if (!isRecording) return;

        Debug.Log("[VoiceRecognizer] Recording stopped");
        Microphone.End(null);
        isRecording = false;

        IsProcessing = true;
        StartCoroutine(SendToFunctions());
    }

    IEnumerator SendToFunctions()
    {
        Debug.Log("[VoiceRecognizer] Converting audio...");

        byte[] wavData = ConvertToWAV(recordedClip);
        string base64Audio = Convert.ToBase64String(wavData);

        string json = $"{{\"audio\": \"{base64Audio}\"}}";

        Debug.Log("[VoiceRecognizer] Sending to Firebase Functions...");

        UnityWebRequest request = new UnityWebRequest(FUNCTIONS_URL, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[VoiceRecognizer] Response: " + request.downloadHandler.text);

            string result = ParseResponse(request.downloadHandler.text);

            if (!string.IsNullOrEmpty(result))
            {
                Debug.Log($"[VoiceRecognizer] Recognized: {result}");
                OnRecognized?.Invoke(result);
            }
            else
            {
                Debug.LogWarning("[VoiceRecognizer] No speech detected");
                OnError?.Invoke("음성이 인식되지 않았습니다");
            }
        }
        else
        {
            Debug.LogError($"[VoiceRecognizer] Error: {request.error}");
            OnError?.Invoke("음성 인식 실패");
        }

        IsProcessing = false;
    }

    byte[] ConvertToWAV(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        short[] intData = new short[samples.Length];
        byte[] pcmData = new byte[samples.Length * 2];

        int rescaleFactor = 32767;
        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * rescaleFactor);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(pcmData, i * 2);
        }

        int sampleRate = clip.frequency;
        int channels = clip.channels;
        int byteRate = sampleRate * channels * 2;
        int dataSize = pcmData.Length;
        int fileSize = 44 + dataSize;

        byte[] wav = new byte[fileSize];

        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BitConverter.GetBytes(fileSize - 8).CopyTo(wav, 4);
        System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);

        System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        BitConverter.GetBytes(16).CopyTo(wav, 16);
        BitConverter.GetBytes((short)1).CopyTo(wav, 20);
        BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
        BitConverter.GetBytes(byteRate).CopyTo(wav, 28);
        BitConverter.GetBytes((short)(channels * 2)).CopyTo(wav, 32);
        BitConverter.GetBytes((short)16).CopyTo(wav, 34);

        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BitConverter.GetBytes(dataSize).CopyTo(wav, 40);
        pcmData.CopyTo(wav, 44);

        return wav;
    }

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
