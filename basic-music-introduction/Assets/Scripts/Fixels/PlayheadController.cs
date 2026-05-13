using UnityEngine;

public class PlayheadController : MonoBehaviour
{
    public static PlayheadController instance;
    void Awake()
    {
        instance = this;
    }

    
    public FixelTextureManager fixelManager;
    public PianoRollRenderer pianoRoll;

    [Header("Timing")]
    public float bpm = 120f;
    public bool isPlaying = false;

    [Header("Visuals")]
    public Color playheadColor = Color.red;
    [Range(0f, 1f)]
    public float transparency = 0.5f;

    private float currentStep = 0f; // Stores the precise horizontal position
    private int lastAppliedColumn = -1;


    public void PlayPause_Button()
    {
        isPlaying = !isPlaying;
        if (!isPlaying) ResetPlayhead();
    }
    
    void Update()
    {
        // 1. Toggle Play/Stop
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlayPause_Button();
        }

        if (isPlaying)
        {
            UpdatePlayhead();
        }
    }

    void UpdatePlayhead()
    {
        // 2. Calculate movement
        // (BPM * 4 steps per beat) / 60 seconds
        float stepsPerSecond = (bpm * 4f) / 60f;
        currentStep += stepsPerSecond * Time.deltaTime;

        // 3. Loop logic (64 columns)
        if (currentStep >= 64f)
        {
            currentStep = 0f;
        }

        int currentColumn = Mathf.FloorToInt(currentStep);

        // 4. Redraw everything (Background + Playhead)
        // We redraw the background every frame so the playhead doesn't "smear"
        pianoRoll.DrawPianoRoll(false); // Pass 'false' so it doesn't call ApplyChanges yet
        DrawPlayhead(currentColumn);
        fixelManager.ApplyChanges(); // Final apply for the frame
    }

    void DrawPlayhead(int x)
    {
        for (int y = 0; y < fixelManager.height; y++)
        {
            // Get the background color that was just drawn
            // (Note: FixelTextureManager needs a GetPixel function or similar)
            Color bgColor = GetBackgroundAt(x, y);

            // Alpha Blending: Result = (Source * Alpha) + (Dest * (1 - Alpha))
            Color blendedColor = Color.Lerp(bgColor, playheadColor, transparency);

            fixelManager.SetFixel(x, y, blendedColor);
        }
    }

    void ResetPlayhead()
    {
        currentStep = 0f;
        pianoRoll.DrawPianoRoll(true); // Redraw clean background
    }

    // Helper to estimate background color for blending
    // (A more advanced version would read from the texture buffer)
    Color GetBackgroundAt(int x, int y)
    {
        // This is a simplified version; in a real project, 
        // you'd pull this from the PianoRoll's calculation logic.
        return pianoRoll.GetColorForPixel(x, y);
    }
}