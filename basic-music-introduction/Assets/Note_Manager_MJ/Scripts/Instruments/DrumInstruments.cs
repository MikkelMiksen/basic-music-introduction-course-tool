using UnityEngine;
using Data_holders.instruments;

namespace Scripts.Instruments
{
    public class KickDrum : MonoBehaviour, IInstrument
    {
        [SerializeField] private float frequency = 50f;
        [SerializeField] private float decay = 5f;
        [SerializeField] private float pitchDrop = 100f;
        
        private float phase;
        private float currentAmplitude;
        private float currentFrequency;
        private double sampleRate;
        private System.Random random = new System.Random();

        void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
        }

        public void Trigger(float velocity)
        {
            currentAmplitude = velocity;
            currentFrequency = frequency + pitchDrop;
            phase = 0;
        }

        public void ProcessAudio(float[] data, int channels)
        {
            if (currentAmplitude <= 0.0001f) return;

            for (int i = 0; i < data.Length; i += channels)
            {
                float sample = Mathf.Sin(phase * 2f * Mathf.PI) * currentAmplitude;
                
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] += sample;
                }

                phase += (float)(currentFrequency / sampleRate);
                if (phase > 1f) phase -= 1f;

                currentAmplitude *= (1f - (decay * (float)(1f / sampleRate)));
                currentFrequency = Mathf.Lerp(currentFrequency, frequency, 10f * (float)(1f / sampleRate));
            }
        }
    }

    public class SnareDrum : MonoBehaviour, IInstrument
    {
        [SerializeField] private float frequency = 180f;
        [SerializeField] private float decay = 10f;
        [SerializeField] private float noiseMix = 0.5f;
        
        private float phase;
        private float currentAmplitude;
        private double sampleRate;
        private System.Random random = new System.Random();

        void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
        }

        public void Trigger(float velocity)
        {
            currentAmplitude = velocity;
            phase = 0;
        }

        public void ProcessAudio(float[] data, int channels)
        {
            if (currentAmplitude <= 0.0001f) return;

            for (int i = 0; i < data.Length; i += channels)
            {
                float tone = Mathf.Sin(phase * 2f * Mathf.PI);
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float sample = ((tone * (1f - noiseMix)) + (noise * noiseMix)) * currentAmplitude;
                
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] += sample;
                }

                phase += (float)(frequency / sampleRate);
                if (phase > 1f) phase -= 1f;

                currentAmplitude *= (1f - (decay * (float)(1f / sampleRate)));
            }
        }
    }

    public class HiHat : MonoBehaviour, IInstrument
    {
        [SerializeField] private float decay = 20f;
        [SerializeField] private float openDecay = 5f;
        
        private float currentAmplitude;
        private float currentDecay;
        private double sampleRate;
        private System.Random random = new System.Random();

        void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
        }

        public void Trigger(float velocity)
        {
            currentAmplitude = velocity;
            currentDecay = decay;
        }

        public void TriggerOpen(float velocity)
        {
            currentAmplitude = velocity;
            currentDecay = openDecay;
        }

        public void ProcessAudio(float[] data, int channels)
        {
            if (currentAmplitude <= 0.0001f) return;

            for (int i = 0; i < data.Length; i += channels)
            {
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float sample = noise * currentAmplitude;
                
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] += sample;
                }

                currentAmplitude *= (1f - (currentDecay * (float)(1f / sampleRate)));
            }
        }
    }
}
