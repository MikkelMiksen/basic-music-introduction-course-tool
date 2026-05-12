using System;
using System.Collections.Generic;
using UnityEngine;
using Data_holders.instruments;

public class PianoInstrument : MonoBehaviour, IInstrument
{
    struct Partial
    {
        public float freq;
        public float phase;
        public float phaseIncrement;
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

    private float sampleRate;

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }

    public void Trigger(float velocity)
    {
        Trigger(velocity, 60);
    }

    public void Trigger(float velocity, int midiNote)
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
                phaseIncrement = (2f * Mathf.PI * freq) / sampleRate,
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
        if (voices.Count == 0) return;

        float twoPi = Mathf.PI * 2f;
        int actualStart = startSample * channels;
        int actualEnd = endSample * channels;

        for (int dataIndex = actualStart; dataIndex < actualEnd; dataIndex += channels)
        {
            float output = 0f;

            for (int v = voices.Count - 1; v >= 0; v--)
            {
                PianoVoice voice = voices[v];
                if (voice.partials == null || voice.partials.Length == 0)
                {
                    continue;
                }

                float voiceSample = 0f;

                for (int i = 0; i < voice.partials.Length; i++)
                {
                    ref Partial p = ref voice.partials[i];

                    p.phase += p.phaseIncrement;
                    if (p.phase > twoPi)
                        p.phase -= twoPi;

                    float s = MathF.Sin(p.phase);
                    voiceSample += s * p.amp;
                    p.amp *= p.decay;
                }

                if (voice.released)
                    voice.env *= 0.9995f;

                voiceSample *= voice.env;
                output += voiceSample;

                if (voice.IsDead)
                    voices.RemoveAt(v);
            }

            output *= 0.25f;
            output = MathF.Tanh(output * 2.0f);

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