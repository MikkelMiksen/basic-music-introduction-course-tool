using UnityEngine;
using System.Collections.Generic;

public class SequencerEngine : MonoBehaviour
{
    public LegoDetector detector;

    [Header("Timing")]
    public int bpm = 120;

    // Internal timing
    private double dspStartTime;
    private double secondsPerBeat;
    private double loopDuration;

    // Grid size (your system)
    private const int GRID_WIDTH = 64;  // time
    private const int GRID_HEIGHT = 48; // pitch

    // Track per-block trigger state
    private Dictionary<int, int> blockLastTrigger = new Dictionary<int, int>();

    void Start()
    {
        secondsPerBeat = 60.0 / bpm;

        // 1 bar of 4/4 → 4 beats
        loopDuration = secondsPerBeat * 4.0;

        dspStartTime = AudioSettings.dspTime;
    }

    void Update()
    {
        double dspTime = AudioSettings.dspTime;
        double elapsed = (dspTime - dspStartTime) % loopDuration;

        // Normalize 0 → 1
        double normalizedTime = elapsed / loopDuration;

        // Convert to grid position (0 → 63)
        double currentX = normalizedTime * GRID_WIDTH;

        ProcessBlocks(currentX);
    }

    void ProcessBlocks(double currentX)
    {
        if (detector == null) return;

        var blocks = detector.detectedBlocks;

        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];

            int blockId = GetBlockId(block);

            int subdivision = GetSubdivision(block.type);

            // Width of one grid column
            double cellWidth = 1.0;

            // Define block region
            double startX = block.gridX;
            double endX = block.gridX + cellWidth;

            // Check if playhead is inside this block
            if (currentX >= startX && currentX < endX)
            {
                double localPos = (currentX - startX) / cellWidth;

                int triggerIndex = Mathf.FloorToInt((float)(localPos * subdivision));

                if (!blockLastTrigger.ContainsKey(blockId))
                    blockLastTrigger[blockId] = -1;

                if (blockLastTrigger[blockId] != triggerIndex)
                {
                    blockLastTrigger[blockId] = triggerIndex;

                    TriggerNote(block);
                }
            }
            else
            {
                // Reset when playhead leaves block
                blockLastTrigger[blockId] = -1;
            }
        }
    }

    int GetSubdivision(LegoDetector.BlockType type)
    {
        switch (type)
        {
            case LegoDetector.BlockType.Quarter:
                return 1;

            case LegoDetector.BlockType.Eighth:
                return 2;

            case LegoDetector.BlockType.Sixteenth:
                return 4;

            default:
                return 1;
        }
    }

    int GetBlockId(LegoDetector.Block block)
    {
        // Simple hash (can improve later)
        return block.gridX * 1000 + block.gridY;
    }

    void TriggerNote(LegoDetector.Block block)
    {
        int pitch = block.gridY;

        Debug.Log($"Play note → pitch: {pitch}, type: {block.type}");

        // TODO: hook into audio system
        // Example later:
        // PlayFrequency(GridToFrequency(pitch));
    }
}