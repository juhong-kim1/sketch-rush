using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DrawingCanvas : MonoBehaviour
{
    [Header("Canvas Settings")]
    [SerializeField] private RawImage drawingArea;
    [SerializeField] private int textureWidth = 512;
    [SerializeField] private int textureHeight = 512;

    [Header("Drawing Settings")]
    [SerializeField] private Color drawColor = Color.black;
    [SerializeField] private int brushSize = 5;

    private Texture2D texture;
    private Color[] clearColors;
    private bool isDrawing = false;
    private Vector2 lastDrawPosition;

    void Start()
    {
        InitializeTexture();
    }

    void Update()
    {
        HandleInput();
    }

    void InitializeTexture()
    {
        texture = new Texture2D(textureWidth, textureHeight);

        clearColors = new Color[textureWidth * textureHeight];
        for (int i = 0; i < clearColors.Length; i++)
        {
            clearColors[i] = Color.white;
        }

        texture.SetPixels(clearColors);
        texture.Apply();

        drawingArea.texture = texture;
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartDrawing(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0))
        {
            ContinueDrawing(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            StopDrawing();
        }

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    StartDrawing(touch.position);
                    break;
                case TouchPhase.Moved:
                    ContinueDrawing(touch.position);
                    break;
                case TouchPhase.Ended:
                    StopDrawing();
                    break;
            }
        }
    }

    void StartDrawing(Vector2 screenPosition)
    {
        Vector2 localPos = GetLocalPosition(screenPosition);
        if (IsWithinCanvas(localPos))
        {
            isDrawing = true;
            lastDrawPosition = localPos;
            DrawPoint(localPos);
        }
    }

    void ContinueDrawing(Vector2 screenPosition)
    {
        if (!isDrawing) return;

        Vector2 localPos = GetLocalPosition(screenPosition);
        if (IsWithinCanvas(localPos))
        {
            DrawLine(lastDrawPosition, localPos);
            lastDrawPosition = localPos;
        }
    }

    void StopDrawing()
    {
        isDrawing = false;
        texture.Apply(); // 마지막에 한 번 더 (안전)
    }

    Vector2 GetLocalPosition(Vector2 screenPosition)
    {
        RectTransform rectTransform = drawingArea.rectTransform;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            screenPosition,
            null,
            out localPoint
        );

        Rect rect = rectTransform.rect;
        float normalizedX = (localPoint.x - rect.x) / rect.width;
        float normalizedY = (localPoint.y - rect.y) / rect.height;

        return new Vector2(
            normalizedX * textureWidth,
            normalizedY * textureHeight
        );
    }

    bool IsWithinCanvas(Vector2 localPos)
    {
        return localPos.x >= 0 && localPos.x < textureWidth &&
               localPos.y >= 0 && localPos.y < textureHeight;
    }

    // Apply 없는 버전 추가
    void DrawPointWithoutApply(Vector2 position)
    {
        int x = (int)position.x;
        int y = (int)position.y;

        for (int i = -brushSize; i <= brushSize; i++)
        {
            for (int j = -brushSize; j <= brushSize; j++)
            {
                if (i * i + j * j <= brushSize * brushSize)
                {
                    int px = x + i;
                    int py = y + j;

                    if (px >= 0 && px < textureWidth && py >= 0 && py < textureHeight)
                    {
                        texture.SetPixel(px, py, drawColor);
                    }
                }
            }
        }
    }

    void DrawPoint(Vector2 position)
    {
        DrawPointWithoutApply(position);
        texture.Apply();
    }

    void DrawLine(Vector2 start, Vector2 end)
    {
        float distance = Vector2.Distance(start, end);
        int steps = Mathf.CeilToInt(distance);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 point = Vector2.Lerp(start, end, t);
            DrawPointWithoutApply(point); // Apply 안 함!
        }

        texture.Apply(); // 한 번만!
    }

    public void SetColor(Color color)
    {
        drawColor = color;
    }

    public void SetBrushSize(int size)
    {
        brushSize = Mathf.Clamp(size, 1, 20);
    }

    public void ClearCanvas()
    {
        texture.SetPixels(clearColors);
        texture.Apply();
    }

    public Texture2D GetTexture()
    {
        return texture;
    }

    public byte[] GetPNG()
    {
        return texture.EncodeToPNG();
    }
}