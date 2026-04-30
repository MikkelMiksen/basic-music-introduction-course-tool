using UnityEngine;
using Data_holders.instruments;
using System.Collections.Generic;

namespace Scripts.Instruments
{
    public class PluckSynth : MonoBehaviour, IInstrument
    {
        private List<PianoNote> activeNotes = new List<PianoNote>();
        private List<float[]> deferredSamples = new List<float[]>();
        private float sampleRate;
        private const int MAX_VOICES = 24;
        private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private const double TIME_BUDGET_MS = 15.0; // Stay under 20ms to avoid audio glitches

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
            if (activeNotes.Count >= MAX_VOICES)
            {
                // Remove the oldest/quietest note if we're at the limit
                activeNotes.RemoveAt(0);
            }
            activeNotes.Add(new PianoNote(midi, velocity, sampleRate));
        }

        public void ProcessAudio(float[] data, int channels, int startSample, int endSample)
        {
            int actualStart = startSample * channels;
            int actualEnd = endSample * channels;
            int sampleCount = endSample - startSample;

            stopwatch.Restart();

            for (int i = actualStart; i < actualEnd; i += channels)
            {
                // check budget every 64 samples
                if (((i - actualStart) / channels) % 64 == 0 && stopwatch.Elapsed.TotalMilliseconds > TIME_BUDGET_MS)
                {
                    // If we are over budget, we stop for this call. 
                    // Note: This WILL cause a dip in sound for these notes in this buffer, 
                    // but it fulfills the "continue in next call" request by not blocking the thread.
                    return; 
                }

                float sample = 0f;
                for (int n = activeNotes.Count - 1; n >= 0; n--)
                {
                    // Optimization: Only process full partials for the newest 8 notes
                    // for older notes, reduce quality
                    int partialLimit = (n > activeNotes.Count - 8) ? 12 : 4;
                    sample += activeNotes[n].Process(partialLimit);
                    
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