using System.Collections.Generic;

namespace Bee.Hub.Core
{
    public sealed class MessageContext
    {
        public IDictionary<string, string> Headers { get; init; }
        public string? MessageId => Headers is null ? null : (Headers.ContainsKey("bh-message-id") ? Headers["bh-message-id"] : null);

        public MessageContext(IDictionary<string, string>? headers = null)
        {
            Headers = headers ?? new Dictionary<string, string>();
        }
    }
}