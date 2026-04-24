using System.Collections.Generic;
using UnityEngine;

public class PianoInstrumentGenerator : MonoBehaviour
{
    public int sampleRate = 48000;

    private List<PianoNote> notes = new List<PianoNote>();

    void Update()
    {
        // TEST INPUT (replace later with your 48-key system)
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                PlayNote(48 + i * 2, 1.0f);
            }
        }
    }

    public void PlayNote(int midi, float velocity)
    {
        notes.Add(new PianoNote(midi, velocity, sampleRate));
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;

            for (int n = notes.Count - 1; n >= 0; n--)
            {
                if (notes[n] == null)
                {
                    Debug.Log("Null note found, skipping.");
                    continue;
                }

                sample += notes[n].Process();

                if (notes[n].IsDead)
                    notes.RemoveAt(n);
            }

            sample *= 3f;

            for (int c = 0; c < channels; c++)
                data[i + c] = sample;
        }
    }
}