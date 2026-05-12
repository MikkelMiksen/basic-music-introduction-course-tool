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

    // subtle spectral presence offset
    private float presenceOffset;

    public bool IsDead => env < 0.001f;

    public PianoNote(int midi, float velocity, float sr)
    {
        sampleRate = sr;

        // =========================================================
        // PRESENCE CURVE
        // =========================================================
        // Low notes = slightly less presence
        // High notes = slightly more presence
        // Entire piano still stays clustered together
        // =========================================================

        float normalized = Mathf.InverseLerp(36f, 96f, midi);

        // Very subtle curve
        presenceOffset = (normalized - 0.5f) * 0.06f;

        float f0 = 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        int count = 12;

        partials = new Partial[count];

        float stiffness = 0.0008f;

        for (int n = 0; n < count; n++)
        {
            int harmonic = n + 1;

            float freq =
                f0 *
                harmonic *
                (1f + stiffness * harmonic * harmonic);

            // =========================================================
            // BASE PIANO SPECTRAL ENVELOPE
            // =========================================================

            float amp =
                Mathf.Exp(-0.25f * harmonic) / harmonic;

            // =========================================================
            // PRESENCE SHAPING
            // =========================================================
            // Only affect upper harmonics slightly.
            // Fundamental remains stable so the piano
            // still sounds coherent.
            // =========================================================

            if (harmonic >= 3)
            {
                float harmonicWeight =
                    Mathf.InverseLerp(3f, count, harmonic);

                amp *= 1f + (presenceOffset * harmonicWeight);
            }

            partials[n] = new Partial
            {
                freq = freq,
                phase = 0f,
                amp = amp * velocity,
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

            p.phase +=
                (float)((2.0 * System.Math.PI * p.freq) / sampleRate);

            if (p.phase > Mathf.PI * 2f)
                p.phase -= Mathf.PI * 2f;

            float s = Mathf.Sin(p.phase);

            output += s * p.amp;

            p.amp *= p.decay;

            partials[i] = p;
        }

        // Keep skipped partials phase synced
        for (int i = count; i < partials.Length; i++)
        {
            var p = partials[i];

            p.phase +=
                (float)((2.0 * System.Math.PI * p.freq) / sampleRate);

            if (p.phase > Mathf.PI * 2f)
                p.phase -= Mathf.PI * 2f;

            p.amp *= p.decay;

            partials[i] = p;
        }

        // Release envelope
        if (released)
            env *= 0.9995f;

        return output * env;
    }

    public void Release()
    {
        released = true;
    }
}