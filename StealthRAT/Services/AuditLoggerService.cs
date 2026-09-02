using System;
using System.IO;

namespace StealthRAT.Services
{
    /// <summary>
    /// Service responsible for logging all incoming remote administrative operations 
    /// to a local log file on the target computer for compliance and auditing.
    /// </summary>
    public static class AuditLoggerService
    {
        private static readonly string AuditFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "session_audit.log");
        private static readonly object FileLock = new object();

        /// <summary>
        /// Logs a remote action to the local audit file.
        /// </summary>
        /// <param name="action">The name of the action performed (e.g. mouseclick, launch).</param>
        /// <param name="details">Additional arguments or metadata for the action.</param>
        public static void LogAction(string action, string details)
        {
            try
            {
                string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [REMOTE ACTION] Action: {action} | Details: {details}{Environment.NewLine}";
                lock (FileLock)
                {
                    File.AppendAllText(AuditFilePath, logLine);
                }
            }
            catch
            {
                // Silently fail to keep main app flow operational
            }
        }

        /// <summary>
        /// Logs an authorization event (e.g. connection allowed or denied).
        /// </summary>
        public static void LogAuthorization(string host, string ip, bool allowed)
        {
            try
            {
                string status = allowed ? "GRANTED" : "DENIED";
                string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [AUTHORIZATION] Remote access request was {status} by local user. Admin: {host} ({ip}){Environment.NewLine}";
                lock (FileLock)
                {
                    File.AppendAllText(AuditFilePath, logLine);
                }
            }
            catch
            {
            }
        }
    }
}
