using System;
using System.Collections.Generic;
using Data_holders.instruments;
using UnityEngine;
using System.Linq;
using Scripts.Instruments;
using Scripts.Audio;

[RequireComponent(typeof(AudioSource))]
public class NoteManager : MonoBehaviour
{
    [SerializeField] private float bpm = 140f;
    [SerializeField] private float gridStepX = 1.0f; // Default to 16th notes
    [SerializeField] private float playheadLoopBarLength = 4f; // Default 4 bars (assuming 4 beats per bar)
    
    [Header("Instrument Assignments (Z-Position)")]
    public float kickZ = 0f;
    public float snareZ = 1f;
    public float closedHiHatZ = 2f;
    public float openHiHatZ = 3f;

    [Header("Master Audio Settings")]
    [SerializeField] private MasterCompressor masterCompressor;

    private List<IInstrument> activeInstruments = new();
    private AudioSource audioSource;
    private double nextTickTime;
    private double sampleRate;
    private int currentTick = 0;
    private int maxTick = 0;
    private bool isPaused = false;
    
    private Dictionary<int, List<Instruments>> sequence = new();

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Find or create instrument instances
        activeInstruments.AddRange(GetComponents<IInstrument>());
        activeInstruments.AddRange(GetComponentsInChildren<IInstrument>());
        activeInstruments = activeInstruments.Distinct().ToList();
        
        if (!activeInstruments.Any())
        {
            Debug.Log("[DEBUG_LOG] No instruments found, adding defaults.");
            activeInstruments.Add(gameObject.AddComponent<KickDrum>());
            activeInstruments.Add(gameObject.AddComponent<SnareDrum>());
            activeInstruments.Add(gameObject.AddComponent<HiHat>());
        }
        else
        {
            Debug.Log($"[DEBUG_LOG] Found {activeInstruments.Count} instruments.");
        }
    }

    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
        nextTickTime = AudioSettings.dspTime;
        if (masterCompressor != null) masterCompressor.Initialize(sampleRate);
        ScanNotes();
        if (audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();
            Debug.Log("[DEBUG_LOG] Started AudioSource playback.");
        }
    }

    public void ScanNotes()
    {
        sequence.Clear();
        int highestTickFound = 0;
        GameObject[] noteObjects = GameObject.FindGameObjectsWithTag("Note");
        
        Debug.Log($"[DEBUG_LOG] Scanning notes. Found {noteObjects.Length} objects with tag 'Note'.");
        
        foreach (GameObject note in noteObjects)
        {
            // Only scan notes that are active and part of an active hierarchy
            if (!note.activeInHierarchy) continue;

            Vector3 pos = note.transform.position;
            int tick = Mathf.RoundToInt(pos.x / gridStepX);
            Instruments inst = GetInstrumentFromZ(pos.z);
            
            if (tick < 0) continue; // Ignore notes with negative X

            if (!sequence.ContainsKey(tick))
            {
                sequence[tick] = new List<Instruments>();
            }
            sequence[tick].Add(inst);

            if (tick > highestTickFound) highestTickFound = tick;
        }
        
        // FL Studio Style: Usually loops by bars. 
        // 1 bar = 16 ticks (at gridStepX = 1.0)
        int barTicks = Mathf.RoundToInt(playheadLoopBarLength * 16 / gridStepX);
        
        // Find the next bar boundary after the highest note
        int totalTicksNeeded = ((highestTickFound / barTicks) + 1) * barTicks;
        
        maxTick = totalTicksNeeded - 1;
        
        Debug.Log($"[DEBUG_LOG] Scan complete. maxTick: {maxTick}, ticks with notes: {sequence.Count}");
    }

    public bool TogglePlayback()
    {
        isPaused = !isPaused;
        if (!isPaused && audioSource != null && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
        return !isPaused;
    }

    private Instruments GetInstrumentFromZ(float z)
    {
        float dKick = Mathf.Abs(z - kickZ);
        float dSnare = Mathf.Abs(z - snareZ);
        float dCHH = Mathf.Abs(z - closedHiHatZ);
        float dOHH = Mathf.Abs(z - openHiHatZ);

        float min = Mathf.Min(dKick, dSnare, dCHH, dOHH);

        if (min == dKick) return Instruments.Kick;
        if (min == dSnare) return Instruments.Snare;
        if (min == dCHH) return Instruments.Closed_HiHat;
        return Instruments.Open_HiHat;
    }

    void Update()
    {
        // Rescan notes if they move (optional, but good for interactive)
        if (Time.frameCount % 60 == 0) 
        {
            ScanNotes();
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (isPaused) return;

        double currentTime = AudioSettings.dspTime;
        double bufferDuration = (double)data.Length / channels / sampleRate;
        double bufferEndTime = currentTime + bufferDuration;

        if (bpm <= 0) bpm = 120f;
        if (gridStepX <= 0) gridStepX = 0.25f;
        double secondsPerTick = (60.0 / bpm) * gridStepX;

        int lastProcessedSample = 0;
        int totalBufferSamples = data.Length / channels;

        int safetyIter = 0;
        while (nextTickTime < bufferEndTime && safetyIter < 100)
        {
            safetyIter++;
            
            double timeUntilTick = nextTickTime - currentTime;
            int tickSampleOffset = (int)(timeUntilTick * sampleRate);
            int currentTickStartSample = Math.Max(lastProcessedSample, Math.Min(totalBufferSamples, tickSampleOffset));

            // 1. Process existing audio up to the tick
            if (currentTickStartSample > lastProcessedSample)
            {
                foreach (var instrument in activeInstruments)
                {
                    instrument.ProcessAudio(data, channels, lastProcessedSample, currentTickStartSample);
                }
            }

            // 2. Trigger new notes
            if (sequence.TryGetValue(currentTick, out var instrumentsToPlay))
            {
                foreach (var instType in instrumentsToPlay)
                {
                    TriggerInstrument(instType);
                }
            }

            currentTick++;
            if (currentTick > maxTick) currentTick = 0;
            
            nextTickTime += secondsPerTick;
            
            if (nextTickTime < currentTime - 1.0) 
            {
                nextTickTime = currentTime; 
            }
            
            lastProcessedSample = currentTickStartSample;
        }

        // 3. Process remaining buffer after last tick
        if (lastProcessedSample < totalBufferSamples)
        {
            foreach (var instrument in activeInstruments)
            {
                instrument.ProcessAudio(data, channels, lastProcessedSample, totalBufferSamples);
            }
        }

        // 4. Apply Master Compression
        if (masterCompressor != null)
        {
            masterCompressor.Process(data, channels);
        }
    }

    private void TriggerInstrument(Instruments type)
    {
        foreach (var inst in activeInstruments)
        {
            if (inst == null) continue;
            if (type == Instruments.Kick && inst is KickDrum) inst.Trigger(0.8f);
            else if (type == Instruments.Snare && inst is SnareDrum) inst.Trigger(0.6f);
            else if (type == Instruments.Closed_HiHat && inst is HiHat hh) hh.Trigger(0.3f);
            else if (type == Instruments.Open_HiHat && inst is HiHat ohh) ohh.TriggerOpen(0.4f);
        }
    }
}
