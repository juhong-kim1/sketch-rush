using UnityEngine;

[CreateAssetMenu(fileName = "APIConfig", menuName = "Config/API Config")]
public class APIConfig : ScriptableObject
{
    [SerializeField] private string claudeApiKey;
    [SerializeField] private string openAIApiKey;
    [SerializeField] private string googleSpeechApiKey;
    public string ClaudeApiKey => claudeApiKey;
    public string OpenAIApiKey => openAIApiKey;
    public string GoogleSpeechApiKey => googleSpeechApiKey;
}