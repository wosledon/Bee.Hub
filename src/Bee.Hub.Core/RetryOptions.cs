using System;

namespace Bee.Hub.Core
{
    public class RetryOptions
    {
        public int MaxRetries { get; set; } = 5;
        public int InitialDelayMs { get; set; } = 200; // initial backoff
        public double BackoffFactor { get; set; } = 2.0; // exponential factor
    }
}
