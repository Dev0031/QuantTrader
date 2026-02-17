using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace QuantTrader.DataIngestion.Services;

/// <summary>In-memory event bus backed by Rx subjects. Suitable for single-process use.</summary>
public sealed class InMemoryEventBus : IEventBus, IDisposable
{
    private readonly ConcurrentDictionary<Type, object> _subjects = new();

    public void Publish<TEvent>(TEvent @event) where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (_subjects.TryGetValue(typeof(TEvent), out var subject))
        {
            ((Subject<TEvent>)subject).OnNext(@event);
        }
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        var subject = (Subject<TEvent>)_subjects.GetOrAdd(typeof(TEvent), _ => new Subject<TEvent>());
        return subject.AsObservable().Subscribe(handler);
    }

    public void Dispose()
    {
        foreach (var subject in _subjects.Values)
        {
            if (subject is IDisposable disposable)
                disposable.Dispose();
        }

        _subjects.Clear();
    }
}
