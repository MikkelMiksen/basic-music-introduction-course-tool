using UnityEngine;

namespace Data_holders.instruments
{
    public interface IInstrument
    {
        void Trigger(float velocity);
        void ProcessAudio(float[] data, int channels, int startSample, int endSample);
    }
}
