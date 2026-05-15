using System.Collections.Generic;
using UnityEngine;
using Data_holders.instruments;

namespace DAW2D
{
    [System.Serializable]
    public class NoteData
    {
        public int tick;        // X position (0-63)
        public int pitch;       // Y position (0-21) - logical pitch for 22 keys
        public int duration;    // Ticks duration
        public int height;      // Vertical span in grid rows
        public float velocity = 0.8f;
    }

    [System.Serializable]
    public class InstrumentSequence
    {
        public Instruments instrument;
        public List<NoteData> notes = new List<NoteData>();
    }

    [System.Serializable]
    public class Pattern
    {
        public string name;
        public List<InstrumentSequence> instrumentSequences = new List<InstrumentSequence>();

        public InstrumentSequence GetOrCreateSequence(Instruments inst)
        {
            var seq = instrumentSequences.Find(s => s.instrument == inst);
            if (seq == null)
            {
                seq = new InstrumentSequence { instrument = inst };
                instrumentSequences.Add(seq);
            }
            return seq;
        }
    }
}
