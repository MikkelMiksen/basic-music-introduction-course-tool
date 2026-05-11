using UnityEngine;

public class PianoRollRenderer : MonoBehaviour
{
    public FixelTextureManager fixelManager;

    [Header("Base Colors")]
    public Color whiteKeyColor = new Color(0.15f, 0.15f, 0.15f);
    public Color blackKeyColor = Color.black;

    [Header("Rhythm Grid Settings")]
    [Range(-1f, 1f)]
    public float barLineBrightness = 0.3f;
    [Range(-1f, 1f)]
    public float beatLineBrightness = 0.1f;

    [Header("Octave Settings")]
    [Range(0, 11)]
    public int startingNoteOffset = 0;

    private readonly bool[] isBlackKeyPattern = {
        false, true,  false, true,  false, false,
        true,  false, true,  false, true,  false
    };

    void Start()
    {
        // This draws the initial background as soon as the game begins
        DrawPianoRoll(true);
    }

    // CHANGE 1: Added 'shouldApply' parameter. 
    // It defaults to true so your old code doesn't break.
    [ContextMenu("Redraw Piano Roll")]
    public void DrawPianoRoll(bool shouldApply = true)
    {
        if (fixelManager == null) return;

        for (int y = 0; y < fixelManager.height; y++)
        {
            for (int x = 0; x < fixelManager.width; x++)
            {
                // Use the new helper function
                fixelManager.SetFixel(x, y, GetColorForPixel(x, y));
            }
        }

        if (shouldApply)
        {
            fixelManager.ApplyChanges();
        }
    }

    // CHANGE 2: This function handles the logic for a single pixel.
    // The Playhead uses this to know what color is "underneath" it.
    public Color GetColorForPixel(int x, int y)
    {
        // Calculate Row (Key) color
        int semitoneIndex = (y / 2) + startingNoteOffset;
        int noteInOctave = semitoneIndex % 12;
        Color baseRowColor = isBlackKeyPattern[noteInOctave] ? blackKeyColor : whiteKeyColor;

        // Calculate Column (Grid) brightness
        float modifier = 0f;
        if (x % 16 == 0) modifier = barLineBrightness;
        else if (x % 4 == 0) modifier = beatLineBrightness;

        return ApplyBrightness(baseRowColor, modifier);
    }

    private Color ApplyBrightness(Color baseColor, float amount)
    {
        return new Color(
            Mathf.Clamp01(baseColor.r + amount),
            Mathf.Clamp01(baseColor.g + amount),
            Mathf.Clamp01(baseColor.b + amount),
            1.0f
        );
    }

    private void OnValidate()
    {
        if (Application.isPlaying && fixelManager != null)
        {
            DrawPianoRoll(true);
        }
    }
}