import numpy as np

def apply_envelope(signal, Fs, attack=0.005, decay=0.1):
    length = len(signal)

    attack_samples = int(Fs * attack)
    decay_samples = int(Fs * decay)

    envelope = np.ones(length)

    # Attack (fade in)
    envelope[:attack_samples] = np.linspace(0, 1, attack_samples)

    # Decay (fade out)
    envelope[-decay_samples:] = np.linspace(1, 0, decay_samples)

    return signal * envelope

def lowpass(signal, alpha=0.1):
    y = np.zeros_like(signal)
    for i in range(1, len(signal)):
        y[i] = alpha * signal[i] + (1 - alpha) * y[i-1]
    return y

def kick(Fs, duration=0.2):
    t = np.linspace(0, duration, int(Fs * duration), False)

    # Pitch sweep (classic analog kick)
    f_start = 120
    f_end = 50
    freq = np.linspace(f_start, f_end, len(t))

    phase = 2 * np.pi * np.cumsum(freq) / Fs
    y = np.sin(phase)

    # Fast decay envelope
    envelope = np.exp(-t * 20)

    return y * envelope

def snare(Fs, duration=0.25):
    t = np.linspace(0, duration, int(Fs * duration), False)

    # Noise (the "snap")
    noise = np.random.randn(len(t))

    # Tone body
    tone = np.sin(2 * np.pi * 180 * t)

    # Envelope
    envelope = np.exp(-t * 15)

    return (0.7 * noise + 0.3 * tone) * envelope

def hihat_closed(Fs, duration=0.05):
    t = np.linspace(0, duration, int(Fs * duration), False)

    noise = np.random.randn(len(t))

    envelope = np.exp(-t * 80)

    return noise * envelope


def hihat_open(Fs, duration=0.25):
    t = np.linspace(0, duration, int(Fs * duration), False)

    # Metallic partials (more inharmonic, bell-like)
    freqs = [500, 900, 1400, 2200, 2800]  # inharmonic ratios
    metallic = sum(np.sin(2 * np.pi * f * t) for f in freqs)

    # Apply exponential decay (faster for higher freqs)
    decay = np.exp(-t * 20)
    metallic *= decay

    # White noise tail
    noise = np.random.randn(len(t))
    noise_env = np.exp(-t * 12)  # slightly faster decay than metallic
    noise = noise * noise_env

    # Mix metallic + noise
    y = 0.6 * metallic + 0.4 * noise

    # Optional: high-pass for brightness (sharp attack)
    y = highpass(y, alpha=0.73)

    # Soft clipping for grit
    y = np.tanh(y * 3.0)

    # Normalize
    y /= np.max(np.abs(y) + 1e-8)

    return y

def highpass(signal, alpha=0.9):
    y = np.zeros_like(signal)
    for i in range(1, len(signal)):
        y[i] = alpha * (y[i-1] + signal[i] - signal[i-1])
    return y

