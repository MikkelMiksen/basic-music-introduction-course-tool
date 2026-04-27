using System.Collections.Generic;
using UnityEngine;

public class PianoInstrumentGenerator : MonoBehaviour
{
    public int sampleRate = 48000;

    private List<PianoNote> notes = new List<PianoNote>();
    private Dictionary<KeyCode, PianoNote> activeKeys = new Dictionary<KeyCode, PianoNote>();

    void Update()
    {
        for (int i = 0; i < 9; i++)
        {
            KeyCode key = KeyCode.Alpha1 + i;

            if (Input.GetKeyDown(key))
            {
                float velocity = 1.0f;

                var note = new PianoNote(48 + i * 2, velocity, sampleRate);

                notes.Add(note);
                activeKeys[key] = note;
            }

            if (Input.GetKeyUp(key))
            {
                if (activeKeys.TryGetValue(key, out var note))
                {
                    note.Release();
                    activeKeys.Remove(key);
                }
            }
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;

            for (int n = notes.Count - 1; n >= 0; n--)
            {
                var note = notes[n];

                sample += note.Process();

                if (note.IsDead)
                    notes.RemoveAt(n);
            }

            // gain staging (important for modal synthesis)
            sample *= 0.25f;

            // limiter
            sample = (float)System.Math.Tanh(sample * 2.0f);

            for (int c = 0; c < channels; c++)
                data[i + c] = sample;
        }
    }
}