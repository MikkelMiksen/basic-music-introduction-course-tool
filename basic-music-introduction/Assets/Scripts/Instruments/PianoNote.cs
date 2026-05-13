using System;
using System.Collections.Generic;
using UnityEngine;
using Data_holders.instruments;

public class PianoInstrument : MonoBehaviour, IInstrument
{
    class Partial
    {
        public float freq;
        public float phase;
        public float amp;
        public float decay;
    }

    class PianoVoice
    {
        public Partial[] partials;

        public float env = 1f;

        public bool released = false;

        public bool IsDead => env < 0.001f;
    }

    private readonly List<PianoVoice> voices = new List<PianoVoice>();

    private readonly float sampleRate;

    private readonly int midiNote;

    public PianoInstrument(int midiNote, float sampleRate)
    {
        this.midiNote = midiNote;
        this.sampleRate = sampleRate;
    }

    public void Trigger(float velocity)
    {
        PianoVoice voice = new PianoVoice();

        float normalized = Mathf.InverseLerp(36f, 96f, midiNote);

        // subtle spectral presence offset
        float presenceOffset = (normalized - 0.5f) * 0.06f;

        float f0 = 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);

        int count = 12;

        voice.partials = new Partial[count];

        float stiffness = 0.0008f;

        for (int n = 0; n < count; n++)
        {
            int harmonic = n + 1;

            float freq =
                f0 *
                harmonic *
                (1f + stiffness * harmonic * harmonic);

            float amp =
                Mathf.Exp(-0.25f * harmonic) / harmonic;

            // brighten upper harmonics slightly for higher notes
            if (harmonic >= 3)
            {
                float harmonicWeight =
                    Mathf.InverseLerp(3f, count, harmonic);

                amp *= 1f + (presenceOffset * harmonicWeight);
            }

            voice.partials[n] = new Partial
            {
                freq = freq,
                phase = 0f,
                amp = amp * velocity,
                decay = 0.9993f - (n * 0.00015f)
            };
        }

        voices.Add(voice);
    }

    public void ProcessAudio(
        float[] data,
        int channels,
        int startSample,
        int endSample)
    {
        for (int sampleIndex = startSample;
             sampleIndex < endSample;
             sampleIndex++)
        {
            float output = 0f;

            for (int v = voices.Count - 1; v >= 0; v--)
            {
                PianoVoice voice = voices[v];

                float voiceSample = 0f;

                for (int i = 0; i < voice.partials.Length; i++)
                {
                    Partial p = voice.partials[i];

                    p.phase +=
                        (float)((2.0 * System.Math.PI * p.freq) / sampleRate);

                    if (p.phase > Mathf.PI * 2f)
                        p.phase -= Mathf.PI * 2f;

                    float s = Mathf.Sin(p.phase);

                    voiceSample += s * p.amp;

                    p.amp *= p.decay;

                    voice.partials[i] = p;
                }

                if (voice.released)
                    voice.env *= 0.9995f;

                voiceSample *= voice.env;

                output += voiceSample;

                if (voice.IsDead)
                    voices.RemoveAt(v);
            }

            // gain staging
            output *= 0.25f;

            // soft limiter
            output = MathF.Tanh(output * 2.0f);

            int dataIndex = sampleIndex * channels;

            for (int c = 0; c < channels; c++)
            {
                data[dataIndex + c] += output;
            }
        }
    }

    public void ReleaseAll()
    {
        for (int i = 0; i < voices.Count; i++)
        {
            voices[i].released = true;
        }
    }
}