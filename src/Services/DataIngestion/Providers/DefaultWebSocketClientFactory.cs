using Websocket.Client;

namespace QuantTrader.DataIngestion.Providers;

/// <summary>Production implementation that creates real Websocket.Client instances.</summary>
public sealed class DefaultWebSocketClientFactory : IWebSocketClientFactory
{
    public IWebsocketClient Create(Uri uri)
    {
        return new WebsocketClient(uri)
        {
            ReconnectTimeout = TimeSpan.FromSeconds(30),
            ErrorReconnectTimeout = TimeSpan.FromSeconds(30)
        };
    }
}
