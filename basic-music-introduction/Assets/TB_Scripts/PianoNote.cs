using UnityEngine;

public class PianoNote
{
    struct Mode
    {
        public float freq;
        public float phase;
        public float amp;
        public float decay;
    }

    private Mode[] modes;
    private float sampleRate;

    private float env = 1f;
    private bool released = false;

    public bool IsDead => env < 0.001f;

    public PianoNote(int midi, float velocity, float sr)
    {
        sampleRate = sr;

        float baseFreq = 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        int numModes = 8;
        modes = new Mode[numModes];

        for (int i = 0; i < numModes; i++)
        {
            float harmonic = i + 1;

            modes[i] = new Mode
            {
                freq = baseFreq * harmonic * (1f + harmonic * 0.0005f), // slight inharmonicity
                phase = 0f,
                amp = 1f / harmonic,
                decay = 0.9995f - (i * 0.0002f)
            };
        }
    }

    public float Process()
    {
        float output = 0f;

        for (int i = 0; i < modes.Length; i++)
        {
            Mode m = modes[i];

            m.phase += (2f * Mathf.PI * m.freq) / sampleRate;

            float osc = Mathf.Sin(m.phase);

            output += osc * m.amp;

            m.amp *= m.decay;

            modes[i] = m;
        }

        ApplyEnvelope();

        return output * env;
    }

    void ApplyEnvelope()
    {
        if (released)
            env *= 0.9995f;
    }

    public void Release()
    {
        released = true;
    }
}