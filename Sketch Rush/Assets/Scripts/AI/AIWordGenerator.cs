using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;

public class AIWordGenerator : MonoBehaviour
{
    private string functionsUrl = "https://generatewords-z3boblsivq-uc.a.run.app";
    private static List<string> usedWords = new List<string>();
    public string[] generatedWords { get; private set; }

    public IEnumerator GenerateWords()
    {
        yield return StartCoroutine(RequestWords(20));

        if (generatedWords != null && generatedWords.Length > 0)
        {
            yield return StartCoroutine(ValidateWords(generatedWords));

            int attempts = 0;
            while (generatedWords.Length < 20 && attempts < 3)
            {
                int needed = 20 - generatedWords.Length;
                string[] currentWords = generatedWords;
                yield return StartCoroutine(RequestAdditionalWords(needed, currentWords));
                attempts++;
            }

            Debug.Log($"[AIWordGenerator] Final: {generatedWords.Length} words");
        }
    }

    IEnumerator RequestWords(int count)
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        int randomSeed = Random.Range(1000, 9999);
        string excludedWords = usedWords.Count > 0 ? string.Join(", ", usedWords) : "없음";

        string prompt = string.Format(@"그림으로 표현하기 좋은 명사 {3}개를 한국어로 생성해줘.

절대 사용 금지 (이미 나온 단어):
{2}

랜덤 시드: {0}
시각: {1}

위 금지 단어와 완전히 다른 새로운 단어만!
건축물, 자연, 도구, 탈것, 식물, 동물, 음식, 의류, 스포츠, 가구, 전자제품, 생활용품 등 다양한 분야에서 자유롭게 선택.

난이도: 중간
조건:
- 2~4음절 명사
- 구체적으로 그릴 수 있는 것
- 추상명사 제외
- 현대적이고 일상적인 단어
- 반드시 실제로 존재하는 단어만

반드시 아래 JSON 형식으로만 응답하세요.
{{""words"": [""단어1"", ""단어2""]}}",
            randomSeed, timestamp, excludedWords, count);

        yield return StartCoroutine(CallFunctions(prompt, 1.25f, 1024, true));
    }

    IEnumerator RequestAdditionalWords(int count, string[] existingWords)
    {
        string excludeList = string.Join(", ", existingWords);
        int randomSeed = Random.Range(1000, 9999);

        string prompt = string.Format(@"그림으로 표현하기 좋은 명사 {0}개를 한국어로 생성해줘.

절대 사용 금지: {1}
랜덤 시드: {2}

조건: 2~4음절 명사, 구체적으로 그릴 수 있는 것, 실제 존재하는 단어만

반드시 아래 JSON 형식으로만 응답하세요.
{{""words"": [""단어1"", ""단어2""]}}",
            count, excludeList, randomSeed);

        string[] backup = generatedWords;
        yield return StartCoroutine(CallFunctions(prompt, 1.25f, 512, true));

        if (generatedWords != null)
        {
            List<string> combined = new List<string>(existingWords);
            foreach (string word in generatedWords)
            {
                if (!combined.Contains(word)) combined.Add(word);
            }
            generatedWords = combined.ToArray();
        }
        else
        {
            generatedWords = backup;
        }
    }

    IEnumerator ValidateWords(string[] words)
    {
        string wordList = string.Join(", ", words);

        string prompt = $@"아래 단어들 중 문제있는 단어를 제거해줘.

단어 목록: {wordList}

제거 기준:
- 존재하지 않는 단어
- 명사가 아닌 것
- 그림으로 그릴 수 없는 추상개념
- 공백 포함 단어

반드시 아래 JSON 형식으로만 응답하세요.
{{""valid"": [""유지할단어""], ""removed"": [""제거단어""]}}";

        yield return StartCoroutine(CallFunctions(prompt, 0.1f, 512, false));
    }

    IEnumerator CallFunctions(string prompt, float temperature, int maxTokens, bool isWordRequest)
    {
        string requestJson = $@"{{
            ""prompt"": {JsonEscape(prompt)},
            ""temperature"": {temperature},
            ""maxTokens"": {maxTokens}
        }}";

        UnityWebRequest request = new UnityWebRequest(functionsUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestJson);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            if (isWordRequest)
                ParseWordsFromResponse(request.downloadHandler.text);
            else
                ParseValidationResponse(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError($"[AIWordGenerator] Request Failed: {request.error}");
        }
    }

    void ParseWordsFromResponse(string response)
    {
        try
        {
            string textContent = ExtractContent(response);
            WordList wordList = JsonUtility.FromJson<WordList>(textContent);

            if (wordList?.words != null && wordList.words.Length > 0)
            {
                generatedWords = wordList.words;
                foreach (string word in generatedWords)
                {
                    if (!usedWords.Contains(word)) usedWords.Add(word);
                }
                Debug.Log($"[AIWordGenerator] Generated {generatedWords.Length} words");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AIWordGenerator] Parse Failed: {e.Message}");
        }
    }

    void ParseValidationResponse(string response)
    {
        try
        {
            string textContent = ExtractContent(response);
            ValidationResult result = JsonUtility.FromJson<ValidationResult>(textContent);

            if (result?.valid != null && result.valid.Length > 0)
            {
                generatedWords = result.valid;
                Debug.Log($"[AIWordGenerator] Validated: {result.valid.Length} words");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AIWordGenerator] Validation Parse Failed: {e.Message}");
        }
    }

    string ExtractContent(string response)
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
            if (response[contentEnd] == '"' && response[contentEnd - 1] != '\\') break;
            contentEnd++;
        }

        string textContent = response.Substring(contentStart, contentEnd - contentStart)
            .Replace("```json\\n", "")
            .Replace("```\\n", "")
            .Replace("\\n", "")
            .Replace("\\r", "")
            .Replace("\\\"", "\"")
            .Replace("  ", "")
            .Trim();

        int jsonStart = textContent.IndexOf('{');
        int jsonEnd = textContent.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
            textContent = textContent.Substring(jsonStart, jsonEnd - jsonStart + 1);

        return textContent;
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
public class WordList { public string[] words; }

[System.Serializable]
public class ValidationResult { public string[] valid; public string[] removed; }