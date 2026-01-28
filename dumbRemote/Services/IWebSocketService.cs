/*
 * =========================================================================================
 * File: IWebSocketService.cs
 * Namespace: dumbRemote.Services
 * Author: Radim Kopunec
 * Description: Interface definition for the WebSocket communication service.
 * Abstraction layer allows for easier testing and maintenance.
 * =========================================================================================
 */

using System;
using System.Threading.Tasks;

namespace dumbRemote.Services
{
    /// <summary>
    /// Defines the contract for WebSocket communication.
    /// </summary>
    public interface IWebSocketService
    {
        /// <summary>
        /// Triggered when the connection to the server is lost or closed.
        /// </summary>
        event EventHandler Disconnected;

        /// <summary>
        /// Triggered when a connection is successfully established.
        /// </summary>
        event EventHandler Connected;

        /// <summary>
        /// Gets the current connection state.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connects to the specified IP address and port.
        /// </summary>
        /// <param name="ipAddress">The target IP address (e.g., "192.168.0.100").</param>
        /// <param name="port">The target port (e.g., 8080).</param>
        Task ConnectAsync(string ipAddress, int port);

        /// <summary>
        /// Closes the connection gracefully.
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Sends a text message to the server.
        /// </summary>
        /// <param name="message">The command string to send.</param>
        Task SendMessageAsync(string message);
    }
}