using UnityEngine;
using System;

public class PianoNote
{
    private float sampleRate;
    private float[][] buffers;
    private int[] indices;
    private int voices = 3;

    private float env = 1f;
    private float release = 2.0f;
    private bool released = false;

    private float damping = 0.995f;
    private float stiffness = 0.0012f;

    private float velocity;

    private float bodyFilter = 0f;
    private float bodyAlpha = 0.08f;

    public bool IsDead => env <= 0.001f && released;

    public PianoNote(int midi, float vel, float sr)
    {
        sampleRate = sr;
        velocity = vel;

        buffers = new float[voices][];
        indices = new int[voices];

        float baseFreq = MidiToFreq(midi);

        for (int v = 0; v < voices; v++)
        {
            float detune = UnityEngine.Random.Range(-1.0f, 1.0f);
            float freq = baseFreq * Mathf.Pow(2f, detune / 1200f);

            int size = Mathf.Clamp((int)(sampleRate / freq), 16, 2048);

            buffers[v] = new float[size];

            ExciteHammer(buffers[v], velocity);

            indices[v] = 0;
        }
    }

    void ExciteHammer(float[] buffer, float vel)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            float t = i / (float)buffer.Length;

            float hammer =
                Mathf.Exp(-t * 180f) *
                Mathf.Sin(2 * Mathf.PI * 120f * t);

            buffer[i] = hammer * vel;
        }
    }

    public float Process()
    {
        float output = 0f;

        for (int v = 0; v < voices; v++)
        {
            float[] buffer = buffers[v];

            int i = indices[v];

            float current = buffer[i];
            int next = (i + 1) % buffer.Length;
            int next2 = (i + 2) % buffer.Length;

            float nextVal = buffer[next];
            float next2Val = buffer[next2];

            float sample =
                0.5f * (current + nextVal)
                + stiffness * (next2Val - current);

            sample *= damping;

            buffer[i] = sample;

            indices[v] = next;

            output += current;
        }

        output *= 1f / Mathf.Sqrt(voices);

        bodyFilter = Mathf.Lerp(bodyFilter, output, bodyAlpha);
        output = bodyFilter;

        ApplyEnvelope();

        return output * env;
    }

    void ApplyEnvelope()
    {
        if (released)
        {
            env -= 1f / (release * sampleRate);
            if (env < 0f) env = 0f;
        }
    }

    float MidiToFreq(int midi)
    {
        return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }

    public void Release()
    {
        released = true;
    }
}