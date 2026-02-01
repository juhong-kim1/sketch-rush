using UnityEngine;

[CreateAssetMenu(fileName = "APIConfig", menuName = "Config/API Config")]
public class APIConfig : ScriptableObject
{
    [SerializeField] private string claudeApiKey;

    public string ClaudeApiKey => claudeApiKey;
}