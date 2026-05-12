using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ===============================================================
/// SOUND SPACE DEBUG VISUALIZER
/// ===============================================================
///
/// WHAT IT DOES:
/// - Finds all active AudioSources in scene
/// - Analyzes:
///     X = Stereo Pan
///     Y = Presence / Nearness
///     Z = Frequency Balance
/// - Draws colored cubes in 3D space
/// - Labels sounds
/// - Draws trails
/// - Fully runtime
///
/// HOW TO USE:
/// 1. Create Empty GameObject
/// 2. Attach this script
/// 3. Press Play
///
/// OPTIONAL:
/// - Assign custom material
/// - Increase maxSources
///
/// UNITY VERSION:
/// Tested logic for modern Unity versions.
///
/// ===============================================================
/// </summary>
public class SoundSpaceDebugger : MonoBehaviour
{
    [Header("Visualization")]
    public float cubeSize = 0.25f;
    public float spaceScale = 10f;
    public bool drawLabels = true;
    public bool drawTrails = true;
    public int trailLength = 32;

    [Header("Analysis")]
    public int spectrumSize = 512;
    public FFTWindow fftWindow = FFTWindow.BlackmanHarris;

    [Header("Update")]
    public float smoothing = 8f;
    public float refreshRate = 0.05f;

    [Header("Filtering")]
    public bool ignoreMuted = true;
    public bool ignoreSilent = true;
    public float silentThreshold = 0.0005f;

    [Header("Colors")]
    public Gradient frequencyGradient;

    class SoundData
    {
        public AudioSource source;

        public float rms;
        public float peak;
        public float pan;
        public float centroid;
        public float brightness;
        public float presence;

        public Vector3 targetPosition;
        public Vector3 currentPosition;

        public Color color;

        public Queue<Vector3> trail = new Queue<Vector3>();

        public float[] spectrum;
        public float[] samples;
    }

    Dictionary<AudioSource, SoundData> soundMap =
        new Dictionary<AudioSource, SoundData>();

    float refreshTimer;

    void Start()
    {
        if (frequencyGradient.colorKeys.Length == 0)
        {
            SetupDefaultGradient();
        }
    }

    void Update()
    {
        refreshTimer += Time.deltaTime;

        if (refreshTimer >= refreshRate)
        {
            refreshTimer = 0f;

            ScanAudioSources();
            AnalyzeAllSources();
        }

        SmoothMovement();
    }

    void SetupDefaultGradient()
    {
        frequencyGradient = new Gradient();

        frequencyGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.red, 0f),
                new GradientColorKey(Color.yellow, 0.33f),
                new GradientColorKey(Color.green, 0.66f),
                new GradientColorKey(Color.cyan, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f,0f),
                new GradientAlphaKey(1f,1f)
            }
        );
    }

    void ScanAudioSources()
    {
        AudioSource[] all = FindObjectsOfType<AudioSource>();

        HashSet<AudioSource> current = new HashSet<AudioSource>(all);

        List<AudioSource> removeList = new List<AudioSource>();

        foreach (var kvp in soundMap)
        {
            if (!current.Contains(kvp.Key))
            {
                removeList.Add(kvp.Key);
            }
        }

        foreach (var r in removeList)
        {
            soundMap.Remove(r);
        }

        foreach (AudioSource src in all)
        {
            if (!soundMap.ContainsKey(src))
            {
                SoundData data = new SoundData();

                data.source = src;
                data.spectrum = new float[spectrumSize];
                data.samples = new float[spectrumSize];

                soundMap.Add(src, data);
            }
        }
    }

    void AnalyzeAllSources()
    {
        foreach (var kvp in soundMap)
        {
            AnalyzeSource(kvp.Value);
        }
    }

    void AnalyzeSource(SoundData data)
    {
        AudioSource src = data.source;

        if (src == null)
            return;

        if (ignoreMuted && src.mute)
            return;

        src.GetOutputData(data.samples, 0);

        float rms = 0f;
        float peak = 0f;

        for (int i = 0; i < data.samples.Length; i++)
        {
            float s = data.samples[i];

            rms += s * s;

            float abs = Mathf.Abs(s);

            if (abs > peak)
                peak = abs;
        }

        rms = Mathf.Sqrt(rms / data.samples.Length);

        data.rms = rms;
        data.peak = peak;

        if (ignoreSilent && rms < silentThreshold)
            return;

        src.GetSpectrumData(data.spectrum, 0, fftWindow);

        //-----------------------------------------
        // SPECTRAL CENTROID
        //-----------------------------------------

        float weightedSum = 0f;
        float total = 0f;

        for (int i = 0; i < data.spectrum.Length; i++)
        {
            weightedSum += i * data.spectrum[i];
            total += data.spectrum[i];
        }

        float centroid = 0f;

        if (total > 0.00001f)
            centroid = weightedSum / total;

        centroid /= data.spectrum.Length;

        data.centroid = centroid;

        //-----------------------------------------
        // BRIGHTNESS
        //-----------------------------------------

        float low = 0f;
        float high = 0f;

        int split = data.spectrum.Length / 2;

        for (int i = 0; i < split; i++)
        {
            low += data.spectrum[i];
        }

        for (int i = split; i < data.spectrum.Length; i++)
        {
            high += data.spectrum[i];
        }

        data.brightness = high / Mathf.Max(low, 0.0001f);

        //-----------------------------------------
        // PAN
        //-----------------------------------------

        float pan = src.panStereo;

        if (src.spatialBlend > 0.01f)
        {
            Vector3 viewport =
                Camera.main.WorldToViewportPoint(src.transform.position);

            pan = (viewport.x - 0.5f) * 2f;
        }

        data.pan = Mathf.Clamp(pan, -1f, 1f);

        //-----------------------------------------
        // PRESENCE
        //-----------------------------------------

        float volumeFactor =
            Mathf.Clamp01(rms * 25f);

        float brightnessFactor =
            Mathf.Clamp01(data.brightness * 2f);

        float distanceFactor = 1f;

        if (Camera.main != null)
        {
            float dist =
                Vector3.Distance(
                    Camera.main.transform.position,
                    src.transform.position);

            distanceFactor =
                Mathf.Clamp01(1f / (dist * 0.1f));
        }

        data.presence =
            (
                volumeFactor * 0.5f +
                brightnessFactor * 0.3f +
                distanceFactor * 0.2f
            );

        //-----------------------------------------
        // POSITION
        //-----------------------------------------

        float x = data.pan;

        float y = Mathf.Lerp(-1f, 1f, data.presence);

        float z = Mathf.Lerp(-1f, 1f, data.centroid);

        data.targetPosition =
            new Vector3(x, y, z) * spaceScale;

        //-----------------------------------------
        // COLOR
        //-----------------------------------------

        data.color =
            frequencyGradient.Evaluate(data.centroid);

        //-----------------------------------------
        // TRAILS
        //-----------------------------------------

        if (drawTrails)
        {
            data.trail.Enqueue(data.currentPosition);

            while (data.trail.Count > trailLength)
            {
                data.trail.Dequeue();
            }
        }
    }

    void SmoothMovement()
    {
        foreach (var kvp in soundMap)
        {
            SoundData data = kvp.Value;

            data.currentPosition =
                Vector3.Lerp(
                    data.currentPosition,
                    data.targetPosition,
                    Time.deltaTime * smoothing);
        }
    }

    void OnDrawGizmos()
    {
        DrawCubeBounds();

        if (soundMap == null)
            return;

        foreach (var kvp in soundMap)
        {
            DrawSound(kvp.Value);
        }
    }

    void DrawCubeBounds()
    {
        Gizmos.color = new Color(1, 1, 1, 0.15f);

        Gizmos.DrawWireCube(
            Vector3.zero,
            Vector3.one * spaceScale * 2f);

        //-----------------------------------------
        // AXES
        //-----------------------------------------

        Gizmos.color = Color.red;

        Gizmos.DrawLine(
            new Vector3(-spaceScale, 0, 0),
            new Vector3(spaceScale, 0, 0));

        Gizmos.color = Color.green;

        Gizmos.DrawLine(
            new Vector3(0, -spaceScale, 0),
            new Vector3(0, spaceScale, 0));

        Gizmos.color = Color.cyan;

        Gizmos.DrawLine(
            new Vector3(0, 0, -spaceScale),
            new Vector3(0, 0, spaceScale));
    }

    void DrawSound(SoundData data)
    {
        if (data.source == null)
            return;

        if (ignoreSilent && data.rms < silentThreshold)
            return;

        //-----------------------------------------
        // CUBE
        //-----------------------------------------

        Gizmos.color = data.color;

        float size =
            cubeSize +
            data.rms * cubeSize * 8f;

        Gizmos.DrawCube(
            data.currentPosition,
            Vector3.one * size);

        //-----------------------------------------
        // TRAILS
        //-----------------------------------------

        if (drawTrails)
        {
            Vector3? previous = null;

            int index = 0;

            foreach (var p in data.trail)
            {
                float alpha =
                    (float)index / trailLength;

                Gizmos.color =
                    new Color(
                        data.color.r,
                        data.color.g,
                        data.color.b,
                        alpha * 0.5f);

                if (previous.HasValue)
                {
                    Gizmos.DrawLine(previous.Value, p);
                }

                previous = p;
                index++;
            }
        }

#if UNITY_EDITOR

        //-----------------------------------------
        // LABELS
        //-----------------------------------------

        if (drawLabels)
        {
            UnityEditor.Handles.color = data.color;

            string label =
                $"{data.source.name}\n" +
                $"RMS:{data.rms:F3}\n" +
                $"PAN:{data.pan:F2}\n" +
                $"PRES:{data.presence:F2}\n" +
                $"FREQ:{data.centroid:F2}";

            UnityEditor.Handles.Label(
                data.currentPosition + Vector3.up * 0.3f,
                label);
        }

#endif
    }
}
