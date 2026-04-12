using UnityEngine;

/// <summary>
/// 모바일 키보드가 올라올 때 UI 요소를 키보드 위로 밀어주는 스크립트.
/// 이 스크립트가 붙은 RectTransform을 키보드 높이만큼 위로 이동시킵니다.
/// </summary>
public class KeyboardAvoider : MonoBehaviour
{
    private RectTransform rectTransform;
    private Vector2 originalAnchoredPosition;
    private Canvas rootCanvas;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        originalAnchoredPosition = rectTransform.anchoredPosition;
        rootCanvas = GetComponentInParent<Canvas>();
        while (rootCanvas != null && !rootCanvas.isRootCanvas)
            rootCanvas = rootCanvas.transform.parent?.GetComponentInParent<Canvas>();
    }

    void Update()
    {
        float keyboardHeight = GetKeyboardHeight();
        float scaleFactor = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        float offset = keyboardHeight / scaleFactor;

        Vector2 target = new Vector2(originalAnchoredPosition.x, originalAnchoredPosition.y + offset);
        rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, target, Time.deltaTime * 15f);
    }

    float GetKeyboardHeight()
    {
#if UNITY_IOS && !UNITY_EDITOR
        return TouchScreenKeyboard.area.height;
#elif UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var window = activity.Call<AndroidJavaObject>("getWindow"))
            using (var decorView = window.Call<AndroidJavaObject>("getDecorView"))
            {
                var rect = new AndroidJavaObject("android.graphics.Rect");
                decorView.Call("getWindowVisibleDisplayFrame", rect);
                int visibleHeight = rect.Call<int>("height");
                float keyboardH = Screen.height - visibleHeight;
                return keyboardH > 50 ? keyboardH : 0f;
            }
        }
        catch
        {
            return 0f;
        }
#else
        return 0f;
#endif
    }
}
