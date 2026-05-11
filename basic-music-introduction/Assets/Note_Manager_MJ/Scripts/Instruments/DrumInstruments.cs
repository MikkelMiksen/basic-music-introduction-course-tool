using System;
using System.Linq;
using UnityEngine;
using Data_holders.instruments;

namespace Scripts.Instruments
{
    public class KickDrum : MonoBehaviour, IInstrument
    {
        private float starting_frequency = 120f;
        private float end_frequency = 50f;
        private float noice_mix = 0.8f;

        private int Fs;
        
        
        
        void Start()
        {
            Fs = AudioSettings.outputSampleRate;
        }

        public void Trigger(float velocity)
        {
           
            // We don't reset gain here to allow for smooth transitions if triggered rapidly
        }

        public void ProcessAudio(float[] data, int channels, int startSample, int endSample)
        {
            
            
            float[] t = MJ_Math.linspace(0, endSample - startSample, (int)AudioSettings.outputSampleRate * (endSample - startSample));
        }
    }

    public class SnareDrum : MonoBehaviour, IInstrument
    {
        

        void Start()
        {
            
        }

        public void Trigger(float velocity)
        {
            
        }

        public void ProcessAudio(float[] data, int channels, int startSample, int endSample)
        {
            
        }
    }

    public class HiHat : MonoBehaviour, IInstrument
    {
        

        void Start()
        {
            
        }

        public void Trigger(float velocity)
        {
            
        }

        public void TriggerOpen(float velocity)
        {
            
        }

        public void ProcessAudio(float[] data, int channels, int startSample, int endSample)
        {
            
            
        }
    }

    public static class MJ_Math
    {
        public static float[] linspace(float startval, float endval, int steps)
        {
            float interval = (endval / MathF.Abs(endval)) * MathF.Abs(endval - startval) / (steps - 1);
            return (from val in Enumerable.Range(0,steps)
                select startval + (val * interval)).ToArray(); 
        }
    }
}
