using UnityEngine;

public class FixelTextureManager : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int width = 64;
    public int height = 48;

    [Header("Scaling")]
    [Tooltip("1 = 1 unit per pixel. 0.5 = 2 units per pixel (Double size).")]
    public float pixelsPerUnit = 1f;

    [Header("Pivot point")]
    [Tooltip("0.5 0.5 will set the pivot point in the middle")]
    public float pivot1 = 1f;
    public float pivot2 = 1f;

    private Texture2D fixelTexture;
    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        InitializeTexture();
    }

    void InitializeTexture()
    {
        // 1. Create a new texture
        fixelTexture = new Texture2D(width, height);

        // 2. Set pixel-perfect settings
        fixelTexture.filterMode = FilterMode.Point; // No blurring
        fixelTexture.wrapMode = TextureWrapMode.Clamp;

        // 3. Create a Sprite from the texture
        Rect rect = new Rect(0, 0, width, height);
        // set pivot point of sprite here:
        Vector2 pivot = new Vector2(pivot1, pivot2);

        // PPU (Pixels Per Unit) = 1 means 1 pixel is exactly 1 Unity unit
        Sprite newSprite = Sprite.Create(fixelTexture, rect, pivot, pixelsPerUnit);
        spriteRenderer.sprite = newSprite;

        // Clear texture to black initially
        ClearGrid(Color.black);
    }

    public void SetFixel(int x, int y, Color color)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return;

        fixelTexture.SetPixel(x, y, color);
    }

    // Call this after you are done making changes for the frame
    public void ApplyChanges()
    {
        fixelTexture.Apply();
    }

    public void ClearGrid(Color color)
    {
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;

        fixelTexture.SetPixels(pixels);
        fixelTexture.Apply();
    }
}