using System;
using UnityEngine;

namespace Scripts.Audio
{
    [Serializable]
    public class MasterCompressor
    {
        [Range(-60f, 0f)] public float thresholdDb = -12f;
        [Range(1f, 20f)] public float ratio = 4f;
        [Range(0.1f, 100f)] public float attackMs = 10f;
        [Range(10f, 1000f)] public float releaseMs = 100f;
        [Range(0f, 24f)] public float makeUpGainDb = 0f;

        private float currentGain = 1f;
        private double sampleRate;

        public void Initialize(double sr)
        {
            sampleRate = sr;
        }

        public void Process(float[] data, int channels)
        {
            if (sampleRate <= 0) return;

            float threshold = (float)Math.Pow(10.0, thresholdDb / 20.0);
            float makeUpGain = (float)Math.Pow(10.0, makeUpGainDb / 20.0);
            float invRatio = 1f / ratio;
            
            // Time constants
            float attackCoef = (float)Math.Exp(-1.0 / (attackMs * 0.001 * sampleRate));
            float releaseCoef = (float)Math.Exp(-1.0 / (releaseMs * 0.001 * sampleRate));

            for (int i = 0; i < data.Length; i += channels)
            {
                // Simple peak detection (taking the max of all channels for the current frame)
                float maxPeak = 0f;
                for (int c = 0; c < channels; c++)
                {
                    float absVal = Math.Abs(data[i + c]);
                    if (absVal > maxPeak) maxPeak = absVal;
                }

                float targetGain = 1f;
                if (maxPeak > threshold)
                {
                    // Compression formula in linear domain to avoid Log/Pow every sample
                    // We can use a simplified version for the 'smooth' blend
                    // ratio = InDb / OutDb  => OutDb = InDb / ratio
                    // Gain = 10^((OutDb - InDb)/20) = 10^((InDb/ratio - InDb)/20) = 10^(InDb/20 * (1/ratio - 1))
                    // Gain = (10^(InDb/20))^(1/ratio - 1) = InLinear^(1/ratio - 1)
                    // Since maxPeak = 10^(InDb/20) / ThresholdLinear? No.
                    
                    // Let's use the DB version but maybe only every few samples or if maxPeak changed significantly?
                    // Actually, let's keep it accurate but use Math.Log instead of Log10 if possible (faster)
                    
                    float overThreshold = maxPeak / threshold;
                    // targetGain = (overThreshold ^ (1/ratio - 1))
                    targetGain = (float)Math.Pow(overThreshold, invRatio - 1f);
                }

                // Smooth the gain changes
                if (targetGain < currentGain)
                {
                    currentGain = targetGain + attackCoef * (currentGain - targetGain);
                }
                else
                {
                    currentGain = targetGain + releaseCoef * (currentGain - targetGain);
                }

                // Apply gain to all channels in the frame
                float finalGain = currentGain * makeUpGain;
                for (int c = 0; c < channels; c++)
                {
                    data[i + c] *= finalGain;
                }
            }
        }
    }
}
