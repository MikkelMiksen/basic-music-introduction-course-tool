using UnityEngine;
using Data_holders.instruments;
using System.Collections.Generic;

namespace Scripts.Instruments
{
    public class PluckSynth : MonoBehaviour, IInstrument
    {
        private List<PianoNote> activeNotes = new List<PianoNote>();
        private float sampleRate;

        void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
        }

        public void Trigger(float velocity)
        {
            // For drum-like trigger without specific pitch, use a default MIDI note
            TriggerNote(60, velocity); // Middle C
        }

        public void TriggerNote(int midi, float velocity)
        {
            activeNotes.Add(new PianoNote(midi, velocity, sampleRate));
        }

        public void ProcessAudio(float[] data, int channels)
        {
            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = 0f;
                for (int n = activeNotes.Count - 1; n >= 0; n--)
                {
                    sample += activeNotes[n].Process();
                    if (activeNotes[n].IsDead)
                        activeNotes.RemoveAt(n);
                }

                // Volume and Limiter
                sample *= 0.2f;
                sample = (float)System.Math.Tanh(sample);

                for (int c = 0; c < channels; c++)
                {
                    data[i + c] += sample;
                }
            }
        }
    }
}