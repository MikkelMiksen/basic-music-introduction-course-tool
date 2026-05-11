using UnityEngine;
using Data_holders.instruments;

namespace Scripts.Instruments
{
    public class KickDrum : MonoBehaviour, IInstrument
    {
        [SerializeField] private float frequency = 50f;
        [SerializeField] private float decay = 5f;
        [SerializeField] private float pitchDrop = 100f;

        [Header("Limiter Settings")]
        [SerializeField] private float threshold = 0.8f;
        [SerializeField] private float releaseTime = 0.1f;
        
        private float phase;
        private float currentAmplitude;
        private float currentFrequency;
        private double sampleRate;
        private System.Random random = new System.Random();

        // Limiter state
        private float currentGain = 1f;

        void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
        }

        public void Trigger(float velocity)
        {
            currentAmplitude = velocity * 0.1f;
            currentFrequency = frequency + pitchDrop;
            phase = 0;
            // We don't reset gain here to allow for smooth transitions if triggered rapidly
        }

        public void ProcessAudio(float[] data, int channels, int startSample, int endSample)
        {
            if (currentAmplitude <= 0.0001f && currentGain >= 0.999f) return;

            int actualStart = startSample * channels;
            int actualEnd = endSample * channels;
            double invSampleRate = 1.0 / sampleRate;
            float amplitudeDecay = (float)(1f - (decay * invSampleRate));
            double freqLerpFactor = 1.0 - System.Math.Exp(-10.0 * invSampleRate);
            
            // Limiter coefficients
            float relCoef = (float)System.Math.Exp(-1.0 / (releaseTime * sampleRate));

            for (int i = actualStart; i < actualEnd; i += channels)
            {
                float rawSample = (float)System.Math.Sin(phase * 2f * System.Math.PI) * currentAmplitude;
                
                // Peak detection and Limiting (Fruity Limiter style simple peak limiter)
                float absSample = System.Math.Abs(rawSample);
                float targetGain = 1f;
                if (absSample > threshold)
                {
                    targetGain = threshold / absSample;
                }

                // Apply Gain with fast attack (instant) and smooth release
                if (targetGain < currentGain)
                {
                    currentGain = targetGain; // Instant attack
                }
                else
                {
                    currentGain = targetGain + relCoef * (currentGain - targetGain); // Exponential release
                }

                float limitedSample = rawSample * currentGain;

                for (int c = 0; c < channels; c++)
                {
                    data[i + c] += limitedSample;
                }

                phase += (float)(currentFrequency * invSampleRate);
                if (phase > 1f) phase -= 1f;

                currentAmplitude *= amplitudeDecay;
                currentFrequency = (float)(currentFrequency + (frequency - currentFrequency) * freqLerpFactor);
            }
        }
    }

    public class SnareDrum : MonoBehaviour, IInstrument
    {
        [SerializeField] private float frequency = 180f;
        [SerializeField] private float decay = 10f;
        [SerializeField] private float noiseMix = 0.3f;
        
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
            currentAmplitude = velocity * 0.1f;
            phase = 0;
        }

        public void ProcessAudio(float[] data, int channels, int startSample, int endSample)
        {
            if (currentAmplitude <= 0.0001f) return;

            int actualStart = startSample * channels;
            int actualEnd = endSample * channels;

            for (int i = actualStart; i < actualEnd; i += channels)
            {
                float tone = (float)System.Math.Sin(phase * 2f * System.Math.PI);
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
        
        // Oscillators for metallic sound (6 square waves is a classic TR-808 approach)
        private float[] phases = new float[6];
        private static readonly float[] Frequencies = { 205f, 369f, 522f, 540f, 612f, 800f };
        
        // High-pass filter state
        private float hpOldSample;
        private float hpOut;

        void Start()
        {
            sampleRate = AudioSettings.outputSampleRate;
        }

        public void Trigger(float velocity)
        {
            currentAmplitude = velocity * 0.1f;
            currentDecay = decay;
            // Reset phase to ensure consistency
            for (int i = 0; i < phases.Length; i++) phases[i] = 0;
            hpOldSample = 0;
            hpOut = 0;
        }

        public void TriggerOpen(float velocity)
        {
            currentAmplitude = velocity * 0.1f;
            currentDecay = openDecay;
            for (int i = 0; i < phases.Length; i++) phases[i] = 0;
            hpOldSample = 0;
            hpOut = 0;
        }

        public void ProcessAudio(float[] data, int channels, int startSample, int endSample)
        {
            if (currentAmplitude <= 0.0001f) return;

            int actualStart = startSample * channels;
            int actualEnd = endSample * channels;
            double invSampleRate = 1.0 / sampleRate;

            // HP filter coefficient (fixed high cutoff for hi-hats)
            float hpFreq = 7000f;
            float dt = (float)invSampleRate;
            float rc = 1f / (hpFreq * 2f * (float)System.Math.PI);
            float alpha = rc / (rc + dt);

            for (int i = actualStart; i < actualEnd; i += channels)
            {
                float mixedOscs = 0f;
                for (int osc = 0; osc < 6; osc++)
                {
                    // Square wave: 1 if phase < 0.5, -1 otherwise
                    mixedOscs += (phases[osc] < 0.5f) ? 1f : -1f;
                    
                    phases[osc] += (float)(Frequencies[osc] * invSampleRate);
                    if (phases[osc] > 1f) phases[osc] -= 1f;
                }
                
                // Add some noise for texture
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                
                // Normalise and apply high-pass filter
                float inputSample = (mixedOscs / 6f) * 0.7f + noise * 0.3f;
                hpOut = alpha * (hpOut + inputSample - hpOldSample);
                hpOldSample = inputSample;

                float sample = hpOut * currentAmplitude;
                
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] += sample;
                }

                currentAmplitude *= (float)(1f - (currentDecay * invSampleRate));
            }
        }
    }
}
