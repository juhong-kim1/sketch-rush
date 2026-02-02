using UnityEngine;

[CreateAssetMenu(fileName = "APIConfig", menuName = "Config/API Config")]
public class APIConfig : ScriptableObject
{
    [SerializeField] private string claudeApiKey;
    [SerializeField] private string openAIApiKey;

    public string ClaudeApiKey => claudeApiKey;
    public string OpenAIApiKey => openAIApiKey;
}