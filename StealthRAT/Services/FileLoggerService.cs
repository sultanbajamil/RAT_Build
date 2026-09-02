using System;
using System.IO;
using StealthRAT.Interfaces;

namespace StealthRAT.Services
{
    public sealed class FileLoggerService : ILoggerService
    {
        private readonly string _logFilePath;
        private readonly object _writeLock = new object();
        private static Action<string>? _uiLogCallback;

        public FileLoggerService(string logFileName = "rat_debug.log")
        {
            _logFilePath = Path.Combine(Path.GetTempPath(), logFileName);
        }

        public static void RegisterUICallback(Action<string> callback)
        {
            _uiLogCallback = callback;
        }

        public void LogInfo(string message)
        {
            WriteEntry("INFO", message);
        }

        public void LogError(string message, Exception? exception = null)
        {
            string fullMessage = exception != null
                ? $"{message} | Exception: {exception.Message}"
                : message;
            WriteEntry("ERROR", fullMessage);
        }

        public void LogWarning(string message)
        {
            WriteEntry("WARN", message);
        }

        private void WriteEntry(string level, string message)
        {
            try
            {
                string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
                lock (_writeLock)
                {
                    File.AppendAllText(_logFilePath, entry);
                }
                _uiLogCallback?.Invoke($"{level} - {message}");
            }
            catch (IOException)
            {
                // Silently ignore logging failures to prevent cascading errors
            }
        }
    }
}
