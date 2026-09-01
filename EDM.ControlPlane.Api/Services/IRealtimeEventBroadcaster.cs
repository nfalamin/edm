using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EDM.ControlPlane.Api.Services
{
    public record RealtimeEventMessage(
        string EventType,
        object Data,
        DateTime TimestampUtc,
        string EventId);

    public interface IRealtimeEventBroadcaster
    {
        Task BroadcastEventAsync(string eventType, object data);
        IAsyncEnumerable<RealtimeEventMessage> SubscribeAsync(CancellationToken cancellationToken);
        int ActiveSubscriberCount { get; }
    }

    public class RealtimeEventBroadcaster : IRealtimeEventBroadcaster
    {
        private readonly ConcurrentDictionary<Guid, Channel<RealtimeEventMessage>> _subscribers = new();

        public int ActiveSubscriberCount => _subscribers.Count;

        public async Task BroadcastEventAsync(string eventType, object data)
        {
            if (string.IsNullOrWhiteSpace(eventType)) return;

            var msg = new RealtimeEventMessage(
                EventType: eventType,
                Data: data,
                TimestampUtc: DateTime.UtcNow,
                EventId: Guid.NewGuid().ToString("N"));

            // Fan-out to all active SSE subscribers asynchronously
            foreach (var kvp in _subscribers)
            {
                var channel = kvp.Value;
                // Non-blocking try-write to channel
                if (!channel.Writer.TryWrite(msg))
                {
                    // Channel might be bounded and full - drop oldest or handle silently
                }
            }

            await Task.CompletedTask;
        }

        public async IAsyncEnumerable<RealtimeEventMessage> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var subId = Guid.NewGuid();
            var channel = Channel.CreateBounded<RealtimeEventMessage>(new BoundedChannelOptions(256)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

            _subscribers.TryAdd(subId, channel);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    RealtimeEventMessage item;
                    try
                    {
                        item = await channel.Reader.ReadAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ChannelClosedException)
                    {
                        break;
                    }

                    yield return item;
                }
            }
            finally
            {
                _subscribers.TryRemove(subId, out _);
                channel.Writer.TryComplete();
            }
        }
    }
}
