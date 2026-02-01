using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Text;
using System.Text.RegularExpressions; // ← 추가

public class AIWordGenerator : MonoBehaviour
{
    private string apiKey;
    private string apiUrl = "https://api.anthropic.com/v1/messages";
    private APIConfig config;

    public string[] generatedWords { get; private set; }

    void Awake()
    {
        config = Resources.Load<APIConfig>("APIConfig");

        if (config != null)
        {
            apiKey = config.ClaudeApiKey;
            Debug.Log("[AIWordGenerator] API 키 로드 성공!");
        }
        else
        {
            Debug.LogError("[AIWordGenerator] APIConfig를 찾을 수 없습니다!");
        }
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(apiKey))
        {
            GenerateWords();
        }
    }

    public void GenerateWords()
    {
        StartCoroutine(RequestWords());
    }

    IEnumerator RequestWords()
    {
        Debug.Log("[AIWordGenerator] AI에게 단어 요청 중...");

        string prompt = @"그림으로 표현하기 좋은 한국어 명사 20개를 생성해줘.

난이도: 중간
- 그릴 수는 있지만 바로 떠올리기 어려운 수준
- 예시: 신기루, 잠수함, 미로, 망원경, 모래시계

조건:
- 2~4음절
- 명사만
- 실물이거나 구체적으로 그릴 수 있는 개념
- 추상명사 제외
- 중복 없음

반드시 아래 JSON 형식으로만 응답:
{""words"": [""단어1"", ""단어2"", ""단어3"", ..., ""단어20""]}";

        string requestJson = $@"{{
            ""model"": ""claude-sonnet-4-20250514"",
            ""max_tokens"": 1024,
            ""messages"": [
                {{
                    ""role"": ""user"",
                    ""content"": {JsonEscape(prompt)}
                }}
            ]
        }}";

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("x-api-key", apiKey);
        request.SetRequestHeader("anthropic-version", "2023-06-01");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[AIWordGenerator] AI 응답 성공!");

            string responseText = request.downloadHandler.text;
            ParseWordsFromResponse(responseText);
        }
        else
        {
            Debug.LogError($"[AIWordGenerator] AI 요청 실패: {request.error}");
            Debug.LogError(request.downloadHandler.text);
        }
    }

    void ParseWordsFromResponse(string response)
    {
        try
        {
            // "content":[{"text":"..."}] 부분에서 text 추출
            int textStart = response.IndexOf("\"text\":\"") + 8;
            int textEnd = response.IndexOf("\"}", textStart);
            string textContent = response.Substring(textStart, textEnd - textStart);

            // ```json ... ``` 제거
            textContent = textContent.Replace("```json\\n", "").Replace("\\n```", "");

            // 이스케이프 문자 처리
            textContent = textContent.Replace("\\\"", "\"");

            Debug.Log("[AIWordGenerator] 추출된 JSON: " + textContent);

            // {"words": [...]} 파싱
            WordList wordList = JsonUtility.FromJson<WordList>(textContent);
            generatedWords = wordList.words;

            Debug.Log($"[AIWordGenerator] 단어 {generatedWords.Length}개 생성 완료!");
            Debug.Log("[AIWordGenerator] 단어 목록: " + string.Join(", ", generatedWords));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AIWordGenerator] JSON 파싱 실패: {e.Message}");
        }
    }

    private string JsonEscape(string text)
    {
        return "\"" + text.Replace("\\", "\\\\")
                          .Replace("\"", "\\\"")
                          .Replace("\n", "\\n")
                          .Replace("\r", "\\r")
                          .Replace("\t", "\\t") + "\"";
    }
}

[System.Serializable]
public class WordList
{
    public string[] words;
}