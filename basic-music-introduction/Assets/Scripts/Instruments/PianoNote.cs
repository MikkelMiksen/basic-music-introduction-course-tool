using UnityEngine;

public class PianoNote
{
    class Partial
    {
        public float freq;
        public float phase;
        public float amp;
        public float decay;
    }

    private Partial[] partials;
    private float sampleRate;
    private float env = 1f;
    private bool released = false;

    public bool IsDead => env < 0.001f;

    public PianoNote(int midi, float velocity, float sr)
    {
        sampleRate = sr;

        float f0 = 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        int count = 12;
        partials = new Partial[count];

        float stiffness = 0.0001f;

        for (int n = 0; n < count; n++)
        {
            int harmonic = n + 1;

            // Standard Young's formel for inharmonicity
            float freq = f0 * harmonic * Mathf.Sqrt(1f + stiffness * harmonic * harmonic);

            // 🎯 piano spectral envelope (important part)
            float amp = Mathf.Exp(-0.25f * harmonic) / harmonic;

            partials[n] = new Partial
            {
                freq = freq,
                phase = 0f,
                amp = amp,
                decay = 0.9993f - (n * 0.00015f)
            };
        }
    }

    public float Process(int partialLimit = 12)
    {
        float output = 0f;
        int count = System.Math.Min(partialLimit, partials.Length);

        for (int i = 0; i < count; i++)
        {
            var p = partials[i];

            p.phase += (float)((2.0 * System.Math.PI * p.freq) / sampleRate);

            float s = (float)System.Math.Sin(p.phase);

            output += s * p.amp;

            p.amp *= p.decay;

            partials[i] = p;
        }

        // Advance phase for skipped partials to keep them in sync if they are re-enabled
        for (int i = count; i < partials.Length; i++)
        {
            var p = partials[i];
            p.phase += (float)((2.0 * System.Math.PI * p.freq) / sampleRate);
            // Wrap fasen for at bevare præcision
            if (p.phase > Mathf.PI * 2) p.phase -= Mathf.PI * 2;
            p.amp *= p.decay;
            partials[i] = p;
        }

        // envelope
        if (released)
            env *= 0.9995f;

        return output * env;
    }

    public void Release()
    {
        released = true;
    }
}