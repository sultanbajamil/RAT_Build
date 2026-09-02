using System;
using System.Text.Json;
using System.Threading.Tasks;
using StealthRAT.Interfaces;
using StealthRAT.Utilities;

namespace StealthRAT.Handlers
{
    /// <summary>
    /// Handles the "showui" command to display the local system monitor form.
    /// </summary>
    public sealed class ShowUIHandler : ICommandHandler
    {
        public string CommandName => "showui";

        public async Task ExecuteAsync(JsonElement payload, CommandContext context)
        {
            string response = UIManager.ShowUI();
            await context.SendTextResponseAsync(JsonSerializer.Serialize(new
            {
                type = "cmd_response",
                success = response.StartsWith("OK:"),
                output = response
            }));
        }
    }

    /// <summary>
    /// Handles the "hideui" command to hide the local system monitor form.
    /// </summary>
    public sealed class HideUIHandler : ICommandHandler
    {
        public string CommandName => "hideui";

        public async Task ExecuteAsync(JsonElement payload, CommandContext context)
        {
            string response = UIManager.HideUI();
            await context.SendTextResponseAsync(JsonSerializer.Serialize(new
            {
                type = "cmd_response",
                success = response.StartsWith("OK:"),
                output = response
            }));
        }
    }
}
