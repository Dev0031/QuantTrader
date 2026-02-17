using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using QuantTrader.Infrastructure.Messaging;

namespace Integration.Tests;

public class EventBusTests
{
    private readonly InMemoryEventBus _eventBus;

    public EventBusTests()
    {
        var logger = Mock.Of<ILogger<InMemoryEventBus>>();
        _eventBus = new InMemoryEventBus(logger);
    }

    private sealed record TestEvent(string Message, int Value);

    [Fact]
    public async Task Test_InMemoryEventBus_PublishSubscribe()
    {
        // Arrange
        TestEvent? receivedEvent = null;
        var tcs = new TaskCompletionSource<TestEvent>();

        await _eventBus.SubscribeAsync<TestEvent>("test.topic", (evt, ct) =>
        {
            receivedEvent = evt;
            tcs.SetResult(evt);
            return Task.CompletedTask;
        });

        var publishedEvent = new TestEvent("Hello", 42);

        // Act
        await _eventBus.PublishAsync(publishedEvent, "test.topic");

        // Assert - wait for handler to complete
        var result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        receivedEvent.Should().NotBeNull();
        result.Message.Should().Be("Hello");
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task Test_InMemoryEventBus_MultipleSubscribers()
    {
        // Arrange
        var receivedMessages = new List<string>();
        var countdown = new CountdownEvent(2);

        await _eventBus.SubscribeAsync<TestEvent>("multi.topic", (evt, ct) =>
        {
            lock (receivedMessages)
            {
                receivedMessages.Add($"Sub1:{evt.Message}");
            }
            countdown.Signal();
            return Task.CompletedTask;
        });

        await _eventBus.SubscribeAsync<TestEvent>("multi.topic", (evt, ct) =>
        {
            lock (receivedMessages)
            {
                receivedMessages.Add($"Sub2:{evt.Message}");
            }
            countdown.Signal();
            return Task.CompletedTask;
        });

        // Act
        await _eventBus.PublishAsync(new TestEvent("Broadcast", 1), "multi.topic");

        // Assert
        countdown.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
        receivedMessages.Should().HaveCount(2);
        receivedMessages.Should().Contain("Sub1:Broadcast");
        receivedMessages.Should().Contain("Sub2:Broadcast");
    }
}
