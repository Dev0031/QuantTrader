namespace QuantTrader.DataIngestion.Services;

/// <summary>Simple in-process event bus for publishing domain events between components.</summary>
public interface IEventBus
{
    /// <summary>Publish an event to all subscribers.</summary>
    void Publish<TEvent>(TEvent @event) where TEvent : class;

    /// <summary>Subscribe to events of a given type.</summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
}
