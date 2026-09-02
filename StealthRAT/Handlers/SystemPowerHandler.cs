using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using StealthRAT.Interfaces;
using StealthRAT.Services;

namespace StealthRAT.Handlers
{
    /// <summary>
    /// Handles the "shutdown" command to safely power off the remote target machine.
    /// </summary>
    public sealed class ShutdownHandler : ICommandHandler
    {
        public string CommandName => "shutdown";

        public async Task ExecuteAsync(JsonElement payload, CommandContext context)
        {
            try
            {
                AuditLoggerService.LogAction("shutdown", "Remote power-off request");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/s /t 0 /f",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(startInfo);
                await context.SendTextResponseAsync(JsonSerializer.Serialize(new
                {
                    type = "cmd_response",
                    success = true,
                    output = "OK: System shutdown initiated."
                }));
            }
            catch (Exception ex)
            {
                await context.SendTextResponseAsync(JsonSerializer.Serialize(new
                {
                    type = "cmd_response",
                    success = false,
                    output = $"ERR: Failed to initiate shutdown - {ex.Message}"
                }));
            }
        }
    }

    /// <summary>
    /// Handles the "reboot" command to safely restart the remote target machine.
    /// </summary>
    public sealed class RebootHandler : ICommandHandler
    {
        public string CommandName => "reboot";

        public async Task ExecuteAsync(JsonElement payload, CommandContext context)
        {
            try
            {
                AuditLoggerService.LogAction("reboot", "Remote restart request");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 0 /f",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(startInfo);
                await context.SendTextResponseAsync(JsonSerializer.Serialize(new
                {
                    type = "cmd_response",
                    success = true,
                    output = "OK: System reboot initiated."
                }));
            }
            catch (Exception ex)
            {
                await context.SendTextResponseAsync(JsonSerializer.Serialize(new
                {
                    type = "cmd_response",
                    success = false,
                    output = $"ERR: Failed to initiate reboot - {ex.Message}"
                }));
            }
        }
    }
}
