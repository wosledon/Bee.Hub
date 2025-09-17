using Confluent.Kafka;

namespace Bee.Hub.Kafka
{
    public class KafkaOptions
    {
        public ProducerConfig ProducerConfig { get; set; } = new ProducerConfig { BootstrapServers = "localhost:9092" };
        public ConsumerConfig ConsumerConfig { get; set; } = new ConsumerConfig { GroupId = "bee-hub-consumer", BootstrapServers = "localhost:9092", AutoOffsetReset = AutoOffsetReset.Earliest };
        public string? DeadLetterTopic { get; set; }
        public Bee.Hub.Core.RetryOptions Retry { get; set; } = new Bee.Hub.Core.RetryOptions();
    }
}
