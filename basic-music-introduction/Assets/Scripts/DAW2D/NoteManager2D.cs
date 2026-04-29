using System.Collections.Generic;
using UnityEngine;
using Data_holders.instruments;
using Scripts.Instruments;
using System.Linq;

namespace DAW2D
{
    [RequireComponent(typeof(AudioSource))]
    public class NoteManager2D : MonoBehaviour
    {
        public PianoRollController controller;
        public float bpm = 120f;
        
        private List<IInstrument> activeInstruments = new();
        private AudioSource audioSource;
        private double nextTickTime;
        private double sampleRate;
        private int currentTick = 0;
        public int CurrentTick => currentTick;
        private bool isPlaying = false;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            activeInstruments.AddRange(GetComponents<IInstrument>());
            activeInstruments.AddRange(GetComponentsInChildren<IInstrument>());
            activeInstruments = activeInstruments.Distinct().ToList();
            
            if (!activeInstruments.Any())
            {
                activeInstruments.Add(gameObject.AddComponent<KickDrum>());
                activeInstruments.Add(gameObject.AddComponent<SnareDrum>());
                activeInstruments.Add(gameObject.AddComponent<HiHat>());
                activeInstruments.Add(gameObject.AddComponent<PluckSynth>());
            }
        }

        void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
            nextTickTime = AudioSettings.dspTime;
            if (audioSource != null && !audioSource.isPlaying) audioSource.Play();
        }

        public void SetPlaying(bool playing)
        {
            isPlaying = playing;
            if (isPlaying)
            {
                nextTickTime = AudioSettings.dspTime;
                currentTick = 0;
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (controller == null) return;

            if (isPlaying)
            {
                double currentTime = AudioSettings.dspTime;
                double bufferDuration = (double)data.Length / channels / sampleRate;
                double bufferEndTime = currentTime + bufferDuration;

                double secondsPerTick = (60.0 / bpm) / 4.0; // Assuming 16th notes grid

                while (nextTickTime < bufferEndTime)
                {
                    TriggerNotesAtTick(currentTick);
                    currentTick = (currentTick + 1) % 64; // Grid width is 64
                    nextTickTime += secondsPerTick;
                }
            }

            foreach (var inst in activeInstruments)
            {
                inst.ProcessAudio(data, channels);
            }
        }

        private void TriggerNotesAtTick(int tick)
        {
            if (controller.currentMode == PianoRollController.PlayMode.Pattern)
            {
                var pattern = controller.patterns[controller.selectedPatternIndex];
                foreach (var sequence in pattern.instrumentSequences)
                {
                    // Filter: Only play the currently selected instrument
                    if (sequence.instrument != controller.selectedInstrument) continue;

                    var notes = sequence.notes.FindAll(n => n.tick == tick);
                    foreach (var note in notes)
                    {
                        if (sequence.instrument == Instruments.PluckSynth)
                        {
                            int baseMidi = 36;
                            int midiNote = baseMidi + (48 - 1 - note.pitch);
                            PlayPreviewNote(midiNote, note.velocity);
                        }
                        else
                        {
                            TriggerInstrument(sequence.instrument, note.velocity);
                        }
                    }
                }
            }
            // Playlist mode logic would go here
        }

        private void TriggerInstrument(Instruments type, float velocity)
        {
            foreach (var inst in activeInstruments)
            {
                if (type == Instruments.Kick && inst is KickDrum) inst.Trigger(velocity);
                if (type == Instruments.Snare && inst is SnareDrum) inst.Trigger(velocity);
                if (type == Instruments.Closed_HiHat && inst is HiHat hh) hh.Trigger(velocity * 0.5f);
                if (type == Instruments.Open_HiHat && inst is HiHat ohh) ohh.TriggerOpen(velocity * 0.7f);
                if (type == Instruments.PluckSynth && inst is PluckSynth ps) ps.TriggerNote(60, velocity); // Default middle C
            }
        }

        public void PlayPreviewNote(int midi, float velocity)
        {
            foreach (var inst in activeInstruments)
            {
                if (inst is PluckSynth ps) ps.TriggerNote(midi, velocity);
            }
        }
    }
}
