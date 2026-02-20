using Websocket.Client;

namespace QuantTrader.DataIngestion.Providers;

/// <summary>
/// Factory seam for creating WebSocket clients.
/// The default implementation creates real <see cref="WebsocketClient"/> instances;
/// the fake implementation injects pre-scripted message sequences for tests.
/// </summary>
public interface IWebSocketClientFactory
{
    /// <summary>Creates a WebSocket client connected to the given URI.</summary>
    IWebsocketClient Create(Uri uri);
}
