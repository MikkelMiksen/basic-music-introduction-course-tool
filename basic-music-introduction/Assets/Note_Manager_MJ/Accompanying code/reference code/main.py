import sounddevice as sd
import numpy as np
import SoundUtils

def kick(beats):
    return "kick",beats

def snare(beats):
    return "snare",beats

def ch(beats):
    return "ch",beats

def oh(beats):
    return "oh",beats


def build_beat(events, build_Fs, build_seconds_per_beat):
    total_beats = sum(beats for _, beats in events)
    total_samples = int(total_beats * build_seconds_per_beat * build_Fs)

    build_output = np.zeros(total_samples, dtype=np.float32)
    cursor = 0

    for drum, beats in events:
        duration = beats * build_seconds_per_beat
        samples = int(duration * build_Fs)

        if drum == "kick":
            y = SoundUtils.kick(Fs, duration)
        elif drum == "snare":
            y = SoundUtils.snare(Fs, duration)
        elif drum == "ch":
            y = SoundUtils.hihat_closed(Fs, duration)
        elif drum == "oh":
            y = SoundUtils.hihat_open(Fs, duration)
        else:
            y = np.zeros(samples)

        build_output[cursor:cursor+samples] += y[:samples]
        cursor += samples

    return build_output

beat = [
    kick(0.25), ch(0.25), ch(0.25), ch(0.25),
    snare(0.5), ch(0.5),
    kick(0.5), ch(0.25), ch(0.25),
    snare(0.5), ch(0.25), ch(0.25),

    kick(0.25), ch(0.25), ch(0.25), ch(0.25),
    snare(0.5), ch(0.5),
    kick(0.5), ch(0.25), ch(0.25),
    snare(0.5), ch(0.25), ch(0.25),

    kick(0.25), ch(0.25), ch(0.25), ch(0.25),
    snare(0.5), ch(0.5),
    kick(0.5), ch(0.25), ch(0.25),
    snare(0.5), ch(0.25), ch(0.25),

    kick(0.25), ch(0.25), ch(0.25), ch(0.25),
    snare(0.5), ch(0.5),
    kick(0.5), ch(0.25), ch(0.25),
    snare(0.5), ch(0.25), ch(0.25),

    kick(0.25), ch(0.25), ch(0.25), ch(0.25),
    snare(0.5), ch(0.5),
    kick(0.5), ch(0.25), ch(0.25),
    snare(0.5), ch(0.25), ch(0.25),

    kick(0.25), ch(0.25), ch(0.25), ch(0.25),
    snare(0.5), ch(0.5),
    kick(0.5), ch(0.25), ch(0.25),
    snare(0.5), ch(0.25), ch(0.25),

    kick(0.25), ch(0.25), ch(0.25), ch(0.25),
    snare(0.5), ch(0.5),
    kick(0.5), ch(0.25), ch(0.25),
    snare(0.5), ch(0.25), ch(0.25),

    kick(0.25), ch(0.25), ch(0.25), ch(0.25),
    snare(0.5), ch(0.5),
    kick(0.5), ch(0.25), ch(0.25),
    snare(0.5), ch(0.25), ch(0.25),
]

bpm = 140
seconds_per_beat = 60 / bpm
Fs = 44100

output = build_beat(beat, Fs, seconds_per_beat)
output /= np.max(np.abs(output))
output = output.reshape(-1,1)

print(type(output))
print(output.dtype)
print(output.shape)
print(np.max(output), np.min(output))

# print("Just trust me bro! I did what you asked sound is here!!")
sd.play(output, 44100)
sd.wait()