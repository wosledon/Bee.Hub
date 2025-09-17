using System.Collections.Generic;

namespace Bee.Hub.Core
{
    public interface IMessage
    {
        IDictionary<string, string> Headers { get; }
        byte[] Payload { get; }
    }
}