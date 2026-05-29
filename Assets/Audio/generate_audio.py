import wave
import struct
import math
import random
import os

SAMPLE_RATE = 44100

def save_wav(filename, samples):
    with wave.open(filename, 'w') as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(2)
        wav_file.setframerate(SAMPLE_RATE)
        for s in samples:
            # Clamp to 16-bit int
            val = int(max(-1.0, min(1.0, s)) * 32767)
            wav_file.writeframes(struct.pack('<h', val))

def generate_wind():
    duration = 0.5
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    for i in range(num_samples):
        progress = i / num_samples
        # High pass / white noise
        noise = random.uniform(-1.0, 1.0)
        # Envelope: quick attack, slow decay
        envelope = math.exp(-progress * 6)
        samples.append(noise * envelope * 0.4)
    save_wav('wind_shoot.wav', samples)

def generate_water():
    # Looping stream, so let's make it 1 second of bubbly noise
    duration = 1.0
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    phase = 0.0
    for i in range(num_samples):
        # Modulating frequency to sound like bubbles
        t = i / SAMPLE_RATE
        freq = 400 + 300 * math.sin(2 * math.pi * 5 * t)
        phase += 2 * math.pi * freq / SAMPLE_RATE
        val = math.sin(phase)
        samples.append(val * 0.3)
    save_wav('water_shoot.wav', samples)

def generate_earth():
    duration = 0.6
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        progress = i / num_samples
        # Low frequency rumble: mix of 80Hz square and 120Hz sine
        freq = max(40, 80 - 40 * progress) # Pitch drops slightly
        wave1 = 1.0 if math.sin(2 * math.pi * freq * t) > 0 else -1.0
        wave2 = math.sin(2 * math.pi * (freq * 1.5) * t)
        val = (wave1 * 0.6 + wave2 * 0.4)
        envelope = math.exp(-progress * 4)
        samples.append(val * envelope * 0.5)
    save_wav('earth_shoot.wav', samples)

def generate_fire():
    # Looping stream crackle
    duration = 1.0
    num_samples = int(SAMPLE_RATE * duration)
    samples = []
    for i in range(num_samples):
        t = i / SAMPLE_RATE
        # Low noise with some distortion
        noise = random.uniform(-1.0, 1.0)
        if random.random() < 0.1: # occasional pops
            noise *= 3.0
        val = max(-1.0, min(1.0, noise))
        samples.append(val * 0.25)
    save_wav('fire_shoot.wav', samples)

if __name__ == '__main__':
    print(f"Generating audio in {os.getcwd()}...")
    generate_wind()
    generate_water()
    generate_earth()
    generate_fire()
    print("Done!")
