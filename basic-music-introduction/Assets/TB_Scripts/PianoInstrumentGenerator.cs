using System.Collections.Generic;
using Data_holders.instruments;
using UnityEngine;

public class PianoInstrumentGenerator : MonoBehaviour
{
    public int sampleRate = 48000;

    // active instruments
    private readonly List<IInstrument> activeInstruments =
        new List<IInstrument>();

    // key -> instrument
    private readonly Dictionary<KeyCode, PianoInstrument> activeKeys =
        new Dictionary<KeyCode, PianoInstrument>();

    void Update()
    {
        for (int i = 0; i < 9; i++)
        {
            KeyCode key = KeyCode.Alpha1 + i;

            int midi = 48 + i * 2;

            // ============================================
            // KEY DOWN
            // ============================================

            if (Input.GetKeyDown(key))
            {
                float velocity = 1.0f;

                PianoInstrument instrument =
                    new PianoInstrument(midi, sampleRate);

                instrument.Trigger(velocity);

                activeInstruments.Add(instrument);

                activeKeys[key] = instrument;
            }

            // ============================================
            // KEY UP
            // ============================================

            if (Input.GetKeyUp(key))
            {
                if (activeKeys.TryGetValue(key, out var instrument))
                {
                    instrument.ReleaseAll();

                    activeKeys.Remove(key);
                }
            }
        }
    }

    // ============================================
    // MASTER AUDIO CALLBACK
    // ============================================

    void OnAudioFilterRead(float[] data, int channels)
    {
        // clear buffer first
        System.Array.Clear(data, 0, data.Length);

        int totalSamples = data.Length / channels;

        // process all instruments
        foreach (var inst in activeInstruments)
        {
            inst.ProcessAudio(
                data,
                channels,
                0,
                totalSamples);
        }
    }
}