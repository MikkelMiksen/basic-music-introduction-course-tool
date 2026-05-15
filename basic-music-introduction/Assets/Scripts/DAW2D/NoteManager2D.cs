﻿using System.Collections.Generic;
using UnityEngine;
using Data_holders.instruments;
using Scripts.Instruments;
using Scripts.Audio;
using System.Linq;

namespace DAW2D
{
    [RequireComponent(typeof(AudioSource))]
    public class NoteManager2D : MonoBehaviour
    {
        public PianoRollController controller;
        public float bpm = 120f;

        [Header("Master Audio Settings")]
        [SerializeField] private MasterCompressor masterCompressor;
        
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
                activeInstruments.Add(gameObject.AddComponent<PianoInstrument>());
            }
        }

        void Start()
        {
            if (controller == null)
            {
                controller = FindFirstObjectByType<PianoRollController>();
                if (controller == null)
                {
                    Debug.LogError("[NoteManager2D] Controller reference is not set and could not be found automatically.");
                }
            }

            sampleRate = AudioSettings.outputSampleRate;
            nextTickTime = AudioSettings.dspTime;
            if (masterCompressor != null) masterCompressor.Initialize(sampleRate);
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

            int lastProcessedSample = 0;
            int totalBufferSamples = data.Length / channels;

            if (isPlaying)
            {
                double currentTime = AudioSettings.dspTime;
                double bufferDuration = (double)data.Length / channels / sampleRate;
                double bufferEndTime = currentTime + bufferDuration;

                double secondsPerTick = (60.0 / bpm) / 4.0; // Assuming 16th notes grid

                while (nextTickTime < bufferEndTime)
                {
                    double timeUntilTick = nextTickTime - currentTime;
                    int tickSampleOffset = (int)(timeUntilTick * sampleRate);
                    int currentTickStartSample = System.Math.Max(lastProcessedSample, System.Math.Min(totalBufferSamples, tickSampleOffset));

                    // 1. Process existing audio up to the tick
                    if (currentTickStartSample > lastProcessedSample)
                    {
                        foreach (var inst in activeInstruments)
                        {
                            inst.ProcessAudio(data, channels, lastProcessedSample, currentTickStartSample);
                        }
                    }

                    // 2. Trigger new notes
                    TriggerNotesAtTick(currentTick);
                    currentTick = (currentTick + 1) % controller.gridWidth;
                    nextTickTime += secondsPerTick;

                    lastProcessedSample = currentTickStartSample;
                }
            }

            // 3. Process remaining buffer after last tick (or entire buffer if not playing)
            if (lastProcessedSample < totalBufferSamples)
            {
                foreach (var inst in activeInstruments)
                {
                    inst.ProcessAudio(data, channels, lastProcessedSample, totalBufferSamples);
                }
            }

            // 4. Apply Master Compression
            if (masterCompressor != null)
            {
                masterCompressor.Process(data, channels);
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
                        int baseMidi = 36;
                        int midiNote = baseMidi + (controller.gridHeight - 1 - note.pitch);
                        PlayPreviewNote(midiNote, note.velocity);
                    }
                }
            }
            // Playlist mode logic would go here
        }

        private void TriggerInstrument(Instruments type, float velocity, int midiNote = 60)
        {
            foreach (var inst in activeInstruments)
            {
                if (type == Instruments.Kick && inst is KickDrum) inst.Trigger(velocity);
                if (type == Instruments.Snare && inst is SnareDrum) inst.Trigger(velocity);
                if (type == Instruments.Closed_HiHat && inst is HiHat hh) hh.Trigger(velocity * 0.5f);
                if (type == Instruments.Open_HiHat && inst is HiHat ohh) ohh.TriggerOpen(velocity * 0.7f);
                if (type == Instruments.PluckSynth && inst is PianoInstrument ps) ps.Trigger(velocity);
            }
        }

        public void PlayPreviewNote(int midi, float velocity)
        {
            // Unified Drum Mapping
            if (midi == 36) TriggerInstrument(Instruments.Kick, velocity);
            else if (midi == 40) TriggerInstrument(Instruments.Snare, velocity);
            else if (midi == 42) TriggerInstrument(Instruments.Closed_HiHat, velocity);
            else if (midi == 44) TriggerInstrument(Instruments.Open_HiHat, velocity);
            else
            {
                TriggerInstrument(Instruments.PluckSynth, velocity, midi);
            }
        }
    }
}