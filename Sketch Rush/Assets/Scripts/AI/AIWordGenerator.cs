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
                Debug.Log("[AIWordGenerator] OpenAI API");
            }
            else
            {
                apiKey = config.ClaudeApiKey;
                apiUrl = "https://api.anthropic.com/v1/messages";
                Debug.Log("[AIWordGenerator] Claude API");
            }
            
            Debug.Log("[AIWordGenerator] API Key OK");
        }
        else
        {
            Debug.LogError("[AIWordGenerator] APIConfig not found");
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
        // 1단계: 단어 20개 생성
        yield return StartCoroutine(RequestWords(20));

        if (generatedWords != null && generatedWords.Length > 0)
        {
            // 2단계: 검증
            yield return StartCoroutine(ValidateWords(generatedWords));

            // 3단계: 20개 안되면 부족분 재생성 (최대 3회)
            int attempts = 0;
            while (generatedWords.Length < 20 && attempts < 3)
            {
                int needed = 20 - generatedWords.Length;
                Debug.Log($"[AIWordGenerator] Need {needed} more words, attempt {attempts + 1}");

                string[] currentWords = generatedWords;
                yield return StartCoroutine(RequestAdditionalWords(needed, currentWords));

                if (generatedWords.Length > currentWords.Length)
                {
                    // 새로운 단어만 검증
                    string[] newWords = new string[generatedWords.Length - currentWords.Length];
                    System.Array.Copy(generatedWords, currentWords.Length, newWords, 0, newWords.Length);
                    yield return StartCoroutine(ValidateWords(newWords));

                    // 검증된 새 단어 + 기존 단어 합치기
                    List<string> combined = new List<string>(currentWords);
                    combined.AddRange(generatedWords);
                    generatedWords = combined.ToArray();
                }

                attempts++;
            }

            Debug.Log($"[AIWordGenerator] Final: {generatedWords.Length} words");
            Debug.Log("[AIWordGenerator] Final Words: " + string.Join(", ", generatedWords));
        }
    }

    IEnumerator RequestWords(int count)
    {
        Debug.Log($"[AIWordGenerator] Requesting {count} words...");

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        int randomSeed = Random.Range(1000, 9999);
        string excludedWords = usedWords.Count > 0 
            ? string.Join(", ", usedWords) 
            : "없음";

        string prompt = string.Format(@"그림으로 표현하기 좋은 명사 {3}개를 한국어로 생성해줘.

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
- 반드시 실제로 존재하는 단어만 사용하세요.
- 새로운 단어를 만들어내지 마세요.
- 조합하여 새로운 단어를 만들지 마세요.
- 자꾸 반복되는 단어: 자전거, 스마트폰, 드론, 세탁기, 모자 → 절대 사용하지 마세요.

반드시 아래 JSON 형식으로만 응답하세요. 다른 텍스트 없이.
{{""words"": [""단어1"", ""단어2""]}}",
            randomSeed, timestamp, excludedWords, count);

        string requestJson = BuildRequestJson(prompt, 1.25f, 1024);

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        SetRequestHeaders(request);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[AIWordGenerator] Response OK");
            ParseWordsFromResponse(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError($"[AIWordGenerator] Request Failed: {request.error}");
            Debug.LogError(request.downloadHandler.text);
        }
    }

    IEnumerator RequestAdditionalWords(int count, string[] existingWords)
    {
        Debug.Log($"[AIWordGenerator] Requesting {count} additional words...");

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        int randomSeed = Random.Range(1000, 9999);
        string excludeList = string.Join(", ", existingWords);

        string prompt = string.Format(@"그림으로 표현하기 좋은 명사 {0}개를 한국어로 생성해줘.

절대 사용 금지 (이미 있는 단어):
{1}

랜덤 시드: {2}
시각: {3}

조건:
- 2~4음절 명사
- 구체적으로 그림으로 그릴 수 있는 것
- 실제로 존재하는 단어만
- 추상명사 제외
- 공백 없는 단어만
- 위 금지 단어와 완전히 다른 단어만

반드시 아래 JSON 형식으로만 응답하세요. 다른 텍스트 없이.
{{""words"": [""단어1"", ""단어2""]}}",
            count, excludeList, randomSeed, timestamp);

        string requestJson = BuildRequestJson(prompt, 1.25f, 512);

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        SetRequestHeaders(request);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[AIWordGenerator] Additional Response OK");

            // 임시로 파싱
            string[] backupWords = generatedWords;
            ParseWordsFromResponse(request.downloadHandler.text);

            // 기존 단어 + 새 단어 합치기
            if (generatedWords != null)
            {
                List<string> combined = new List<string>(existingWords);
                foreach (string word in generatedWords)
                {
                    if (!combined.Contains(word))
                    {
                        combined.Add(word);
                    }
                }
                generatedWords = combined.ToArray();
                Debug.Log($"[AIWordGenerator] Combined: {generatedWords.Length} words");
            }
            else
            {
                generatedWords = backupWords;
            }
        }
        else
        {
            Debug.LogError($"[AIWordGenerator] Additional Request Failed: {request.error}");
        }
    }

    IEnumerator ValidateWords(string[] words)
    {
        Debug.Log("[AIWordGenerator] Validating words...");

        string wordList = string.Join(", ", words);

        string prompt = $@"아래 단어들 중에서 문제가 있는 단어를 찾아줘. 엄격하게 판단하세요.

단어 목록: {wordList}

반드시 제거할 것:
- 실제로 존재하지 않는 단어 (예: 성간)
- 형용사, 부사, 동사 (명사가 아닌 것)
- 구체적으로 그림으로 그릴 수 없는 개념 (예: 스타트업, 피사체, 샘플러)
- 끝에 불필요한 문자가 붙은 단어 (예: 네일아트를)
- 공백이 포함된 단어 (예: 잠자는 고양이)

유지할 것:
- 구체적으로 그림으로 그릴 수 있는 명사
- 예: 오토바이, 냉장고, 구급차, 의자, 교통수단, 악세서리

반드시 아래 JSON 형식으로만 응답하세요. 다른 텍스트 없이.
{{""valid"": [""유지할단어""], ""removed"": [""제거단어""]}}";

        string requestJson = BuildRequestJson(prompt, 0.1f, 512);

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        SetRequestHeaders(request);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("[AIWordGenerator] Validation OK");
            ParseValidationResponse(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError($"[AIWordGenerator] Validation Failed: {request.error}");
        }
    }

    // API 요청 JSON 빌드 (공통)
    string BuildRequestJson(string prompt, float temperature, int maxTokens)
    {
        if (useOpenAI)
        {
            return $@"{{
                ""model"": ""gpt-4o-mini"",
                ""temperature"": {temperature},
                ""top_p"": 1.0,
                ""max_tokens"": {maxTokens},
                ""messages"": [
                    {{
                        ""role"": ""system"",
                        ""content"": ""You are a Korean word generator for a drawing game. Respond ONLY with valid JSON. No other text.""
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
            return $@"{{
                ""model"": ""claude-sonnet-4-20250514"",
                ""max_tokens"": {maxTokens},
                ""temperature"": {temperature},
                ""messages"": [
                    {{
                        ""role"": ""user"",
                        ""content"": {JsonEscape(prompt)}
                    }}
                ]
            }}";
        }
    }

    // 요청 헤더 세팅 (공통)
    void SetRequestHeaders(UnityWebRequest request)
    {
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
    }

    // 응답에서 content 텍스트 추출 (공통)
    string ExtractContent(string response)
    {
        string textContent;

        if (useOpenAI)
        {
            string searchKey = "\"content\": \"";
            int contentStart = response.IndexOf(searchKey);
            if (contentStart == -1)
            {
                searchKey = "\"content\":\"";
                contentStart = response.IndexOf(searchKey);
            }
            contentStart += searchKey.Length;

            int contentEnd = contentStart;
            while (contentEnd < response.Length)
            {
                if (response[contentEnd] == '"' && response[contentEnd - 1] != '\\')
                    break;
                contentEnd++;
            }
            textContent = response.Substring(contentStart, contentEnd - contentStart);
        }
        else
        {
            string searchKey = "\"text\": \"";
            int textStart = response.IndexOf(searchKey);
            if (textStart == -1)
            {
                searchKey = "\"text\":\"";
                textStart = response.IndexOf(searchKey);
            }
            textStart += searchKey.Length;
            int textEnd = response.IndexOf("\"}", textStart);
            textContent = response.Substring(textStart, textEnd - textStart);
        }

        // 클린업
        textContent = textContent
            .Replace("```json\\n", "")
            .Replace("```\\n", "")
            .Replace("\\n", "")
            .Replace("\\r", "")
            .Replace("\\\"", "\"")
            .Replace("  ", "")
            .Trim();

        // { ~ } 사이만 추출
        int jsonStart = textContent.IndexOf('{');
        int jsonEnd = textContent.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            textContent = textContent.Substring(jsonStart, jsonEnd - jsonStart + 1);
        }

        return textContent;
    }

    void ParseValidationResponse(string response)
    {
        try
        {
            string textContent = ExtractContent(response);
            Debug.Log("[AIWordGenerator] Validation JSON: " + textContent);

            ValidationResult result = JsonUtility.FromJson<ValidationResult>(textContent);

            if (result != null && result.valid != null && result.valid.Length > 0)
            {
                generatedWords = result.valid;
                Debug.Log($"[AIWordGenerator] Validated: {result.valid.Length} words kept");
                Debug.Log("[AIWordGenerator] Kept: " + string.Join(", ", result.valid));

                if (result.removed != null && result.removed.Length > 0)
                {
                    Debug.Log("[AIWordGenerator] Removed: " + string.Join(", ", result.removed));
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AIWordGenerator] Validation Parse Failed: {e.Message}");
        }
    }

    void ParseWordsFromResponse(string response)
    {
        try
        {
            string textContent = ExtractContent(response);
            Debug.Log("[AIWordGenerator] Parsed JSON: " + textContent);

            WordList wordList = JsonUtility.FromJson<WordList>(textContent);

            if (wordList == null || wordList.words == null || wordList.words.Length == 0)
            {
                Debug.LogError("[AIWordGenerator] Word list is empty");
                return;
            }

            generatedWords = wordList.words;

            foreach (string word in generatedWords)
            {
                if (!usedWords.Contains(word))
                {
                    usedWords.Add(word);
                }
            }

            Debug.Log($"[AIWordGenerator] Generated {generatedWords.Length} words");
            Debug.Log("[AIWordGenerator] Words: " + string.Join(", ", generatedWords));
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AIWordGenerator] Parse Failed: {e.Message}");
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

[System.Serializable]
public class ValidationResult
{
    public string[] valid;
    public string[] removed;
}