using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bee.Hub.Core;

namespace Bee.Hub.InMemory
{
    internal class InMemoryTransport : ITransport
    {
        // topic -> list of subscriber channels (one per subscriber)
        private readonly ConcurrentDictionary<string, List<Channel<(byte[] Payload, IDictionary<string, string>? Headers)>>> _subs = new();
        private bool _started = false;

        public Task StartAsync()
        {
            _started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _started = false;
            // complete all channels
            foreach (var kv in _subs)
            {
                lock (kv.Value)
                {
                    foreach (var ch in kv.Value)
                    {
                        ch.Writer.TryComplete();
                    }
                }
            }
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _subs.Clear();
        }

        public Task PublishAsync(string topic, byte[] payload, IDictionary<string, string>? headers = null)
        {
            if (!_started) throw new InvalidOperationException("Transport not started");

            if (_subs.TryGetValue(topic, out var channels))
            {
                lock (channels)
                {
                    foreach (var ch in channels)
                    {
                        // best-effort write; if a writer is closed skip
                        _ = ch.Writer.TryWrite((payload, headers));
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string topic, Func<byte[], IDictionary<string, string>?, Task> handler)
        {
            var ch = Channel.CreateUnbounded<(byte[] Payload, IDictionary<string, string>? Headers)>();
            var list = _subs.GetOrAdd(topic, _ => new List<Channel<(byte[] Payload, IDictionary<string, string>? Headers)>>());
            lock (list)
            {
                list.Add(ch);
            }

            // start reader task
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var item in ch.Reader.ReadAllAsync())
                    {
                        try
                        {
                            await handler(item.Payload, item.Headers);
                        }
                        catch
                        {
                            // swallow handler exceptions to avoid killing the reader loop
                        }
                    }
                }
                catch
                {
                    // reader loop ended
                }
                finally
                {
                    // remove channel from list when finished
                    if (_subs.TryGetValue(topic, out var lst))
                    {
                        lock (lst)
                        {
                            lst.Remove(ch);
                        }
                    }
                }
            });

            return Task.CompletedTask;
        }
    }
}