import socket
import pyaudio

TARGET_IP = "192.168.78.1"   # Replace with controlled laptop's IP
AUDIO_PORT = 9092

CHUNK = 1024
FORMAT = pyaudio.paInt16
CHANNELS = 1
RATE = 16000

p = pyaudio.PyAudio()
stream = p.open(format=FORMAT,
                channels=CHANNELS,
                rate=RATE,
                output=True,
                frames_per_buffer=CHUNK)

sock = socket.socket()
sock.connect((TARGET_IP, AUDIO_PORT))
print("Connected to audio stream. Press Ctrl+C to stop.")
try:
    while True:
        data = sock.recv(CHUNK * 2)
        if not data:
            break
        stream.write(data)
except KeyboardInterrupt:
    pass
finally:
    sock.close()
    stream.stop_stream()
    stream.close()
    p.terminate()