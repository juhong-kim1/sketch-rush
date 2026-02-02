using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;

public class AIWordGenerator : MonoBehaviour
{
    [Header("API Selection")]
    [SerializeField] private bool useOpenAI = true;
    
    private string apiKey;
    private string apiUrl;
    private APIConfig config;

    private static List<string> usedWords = new List<string>();

    public string[] generatedWords { get; private set; }

    void Awake()
    {
        config = Resources.Load<APIConfig>("APIConfig");

        if (config != null)
        {
            if (useOpenAI)
            {
                apiKey = config.OpenAIApiKey;
                apiUrl = "https://api.openai.com/v1/chat/completions";
                Debug.Log("[AIWordGenerator] OpenAI API 사용");
            }
            else
            {
                apiKey = config.ClaudeApiKey;
                apiUrl = "https://api.anthropic.com/v1/messages";
                Debug.Log("[AIWordGenerator] Claude API 사용");
            }
            
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
        StartCoroutine(GenerateWords());
    }
}

    public IEnumerator GenerateWords()
    {
        yield return StartCoroutine(RequestWords());
    }

    IEnumerator RequestWords()
    {
        Debug.Log("[AIWordGenerator] AI에게 단어 요청 중...");

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        int randomSeed = Random.Range(1000, 9999);
        string excludedWords = usedWords.Count > 0 
            ? string.Join(", ", usedWords) 
            : "없음";

        string prompt = string.Format(@"그림으로 표현하기 좋은 명사 20개를 한국어로 생성해줘.

절대 사용 금지 (이미 나온 단어):
{2}

랜덤 시드: {0}
시각: {1}

위 금지 단어와 완전히 다른 새로운 단어만!
건축물, 자연, 도구, 탈것, 식물, 동물, 음식, 의류, 스포츠, 가구, 전자제품, 생활용품 등 다양한 분야에서 자유롭게 선택.

예시:
좋은 예: 헬리콥터, 선인장, 카메라, 트럭, 피자, 기타, 농구공
나쁜 예: 나막신, 솟대, 베틀 같은 전통 단어는 피할 것

난이도: 중간
- 그릴 수 있지만 바로 떠올리기 어려운 수준
- 너무 흔한 단어 제외

조건:
- 2~4음절 명사
- 구체적으로 그릴 수 있는 것
- 추상명사 제외
- 현대적이고 일상적인 단어

JSON 형식:
{{""words"": [""단어1"", ""단어2"", ..., ""단어20""]}}",
            randomSeed, timestamp, excludedWords);

        string requestJson;
        
        if (useOpenAI)
        {
            // OpenAI 요청 형식
            requestJson = $@"{{
                ""model"": ""gpt-4o-mini"",
                ""temperature"": 1.8,
                ""max_tokens"": 500,
                ""messages"": [
                    {{
                        ""role"": ""system"",
                        ""content"": ""You are a creative word generator for drawing games. Always respond in valid JSON format.""
                    }},
                    {{
                        ""role"": ""user"",
                        ""content"": {JsonEscape(prompt)}
                    }}
                ]
            }}";
        }
        else
        {
            // Claude 요청 형식
            requestJson = $@"{{
                ""model"": ""claude-sonnet-4-20250514"",
                ""max_tokens"": 1024,
                ""temperature"": 1.0,
                ""messages"": [
                    {{
                        ""role"": ""user"",
                        ""content"": {JsonEscape(prompt)}
                    }}
                ]
            }}";
        }

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();

        request.SetRequestHeader("Content-Type", "application/json");
        
        if (useOpenAI)
        {
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        }
        else
        {
            request.SetRequestHeader("x-api-key", apiKey);
            request.SetRequestHeader("anthropic-version", "2023-06-01");
        }

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
            string textContent;
            
            if (useOpenAI)
            {
                // OpenAI 응답: choices[0].message.content
                int contentStart = response.IndexOf("\"content\":\"") + 11;
                int contentEnd = response.IndexOf("\"", contentStart);
                
                // 이스케이프된 따옴표 처리
                int depth = 0;
                for (int i = contentStart; i < response.Length; i++)
                {
                    if (response[i] == '{') depth++;
                    if (response[i] == '}') depth--;
                    if (response[i] == '"' && i > 0 && response[i-1] != '\\' && depth == 0)
                    {
                        contentEnd = i;
                        break;
                    }
                }
                
                textContent = response.Substring(contentStart, contentEnd - contentStart);
            }
            else
            {
                // Claude 응답: content[0].text
                int textStart = response.IndexOf("\"text\":\"") + 8;
                int textEnd = response.IndexOf("\"}", textStart);
                textContent = response.Substring(textStart, textEnd - textStart);
            }

            // 공통 처리
            textContent = textContent.Replace("```json\\n", "")
                                     .Replace("\\n```", "")
                                     .Replace("\\n", "")
                                     .Replace("\\r", "")
                                     .Replace("  ", "")
                                     .Replace("\\\"", "\"");

            Debug.Log("[AIWordGenerator] 추출된 JSON: " + textContent);

            WordList wordList = JsonUtility.FromJson<WordList>(textContent);
            generatedWords = wordList.words;

            // 사용한 단어 저장
            foreach (string word in generatedWords)
            {
                if (!usedWords.Contains(word))
                {
                    usedWords.Add(word);
                }
            }

            Debug.Log($"[AIWordGenerator] 단어 {generatedWords.Length}개 생성 완료!");
            Debug.Log("[AIWordGenerator] 단어 목록: " + string.Join(", ", generatedWords));
            Debug.Log($"[AIWordGenerator] 총 사용된 단어: {usedWords.Count}개");
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