# Bee.Hub

Bee.Hub 是一个轻量级、可扩展的消息/事件总线库，支持多种传输（InMemory、Kafka、RabbitMQ）和持久化（EF Core Outbox/Inbox）。设计目标是把消息传递与应用域逻辑解耦，提供事务一致性的 Outbox 模式、可插拔的序列化器与传输实现，以及对 ASP.NET Core 的无缝集成。

## 模块概览

- `Bee.Hub.Core`：核心抽象（`ITransport`, `IMessageSerializer`, 消息与事件契约）和默认实现（`DefaultJsonSerializer`）。所有其他模块依赖此包。
- `Bee.Hub.InMemory`：基于 .NET `System.Threading.Channels` 的内存传输，适用于测试与单进程场景。
- `Bee.Hub.Kafka`：Kafka 传输实现（基于 Confluent.Kafka），用于高吞吐分布式场景。
- `Bee.Hub.RabbitMQ`：RabbitMQ 传输实现（基于 RabbitMQ.Client）。
- `Bee.Hub.AspNetCore`：ASP.NET Core 集成（中间件、HostedService、SignalR 桥接示例），帮助在 Web 应用中接收/桥接消息。
- `Bee.Hub.EfCore`：EF Core 支持，包含 Outbox/Inbox 实体、SaveChanges 拦截器、可替换的 `IOutboxStore`/`IInboxStore` 和后台 Dispatcher（发送/重试/DeadLetter）。

## 快速开始

1. 参考你的宿主应用（ASP.NET Core Console/Worker）添加 NuGet 引用（或项目引用）到需要的模块：
   - 必要：`Bee.Hub.Core`
   - 依据需要：`Bee.Hub.InMemory` / `Bee.Hub.Kafka` / `Bee.Hub.RabbitMQ` / `Bee.Hub.EfCore` / `Bee.Hub.AspNetCore`

2. 在 `Program.cs` 注册序列化器与传输：

```csharp
// ...在 Host 创建处
builder.Services.AddSingleton<IMessageSerializer, DefaultJsonSerializer>();

// InMemory transport (for dev/tests)
builder.Services.AddSingleton<ITransport, InMemoryTransport>();

// Kafka (example)
// builder.Services.AddSingleton<ITransport>(sp => new KafkaTransport(kafkaOptions));

// RabbitMQ (example)
// builder.Services.AddSingleton<ITransport>(sp => new RabbitMqTransport(rabbitOptions));
```

3. 如果你使用 EF Core Outbox（推荐生产）

- 在 `Startup` 或 `Program.cs` 中注册 `BeeHubDbContext`（由应用负责选择 provider），并把 `OutboxSaveChangesInterceptor` 注入到 `DbContextOptions`。示例：

```csharp
services.AddScoped<OutboxSaveChangesInterceptor>();

services.AddDbContext<BeeHubDbContext>((sp, options) =>
{
    options.UseSqlite("Data Source=beehub.db");
    var interceptor = sp.GetRequiredService<OutboxSaveChangesInterceptor>();
    options.AddInterceptors(interceptor);
});

// 注册 EfCore store + dispatcher
services.AddBeeHubEfCore(); // 该扩展注册 IOutboxStore/IInboxStore 和 BackgroundOutboxDispatcher
```

注意：`AddBeeHubEfCore()` 不自动注册 `DbContext` provider，目的是让宿主控制数据库选择与连接字符串。

4. 业务代码中，不需要手动写入 Outbox：
   - 在你的实体或聚合根中，使用 domain events 集合（项目中的约定/接口），`OutboxSaveChangesInterceptor` 会在 `SaveChanges` 时自动把事件序列化并写入 Outbox 表（事务内）。

5. 后台 Dispatcher 会负责读取 Pending 的 Outbox，调用 `ITransport.PublishAsync` 发送消息并批量标记为 Sent / DeadLetter；重试、AttemptCount、DeadLetter 信息均由 `Bee.Hub.EfCore` 管理。

## 重要实现细节与注意事项

- Outbox 自动写入
  - `OutboxSaveChangesInterceptor` 会把域事件序列化为 `OutboxMessage` 并通过 `IOutboxStore.AddToContext` 添加到当前 DbContext，从而保证与业务数据的事务一致性。

- Payload 与 TransportMetadata
  - `OutboxMessage.Payload` 存储为 `byte[]`，Dispatcher 会把 `IMessageSerializer.Serialize` 的结果直接传给 `ITransport.PublishAsync`（避免额外 json 字符串转换开销）。
  - `OutboxMessage.TransportMetadata` 目前以字符串存储：当 `DeadLetter` 时，系统会写入一个序列化的 `DeadLetterInfo`（如果使用 `IMessageSerializer` 会以二进制序列化后 Base64 存储，否则使用文本 JSON）。如果你希望直接存储二进制字段以避免 Base64，可以把字段改为 `byte[]` 并调整数据库迁移（建议用于高性能场景）。

- Batch 操作
  - Dispatcher 使用批量读取 `GetPendingBatchAsync`，并在发送结果后使用 `MarkSentBatchAsync`、`IncrementAttemptBatchAsync`、`MarkDeadLetterBatchAsync` 等批量 API 来减少 DB I/O。

- 可替换性
  - `IOutboxStore` / `IInboxStore` 是接口，你可以用自定义存储实现（例如使用 Dapper、原生 SQL、或第三方数据库）替换默认 EF Core 实现。

## ASP.NET Core 集成

- `Bee.Hub.AspNetCore` 提供了中间件和 SignalR 桥接示例，可把来至外部的消息路由到 SignalR clients 或应用内部处理。将 `Bee.Hub.AspNetCore` 引入宿主并注册其服务后，SignalR hub、HostedService 等都会就绪。

## 调试与故障排查

- 开发环境下优先使用 `InMemoryTransport` 来快速验证消息流与业务逻辑。
- 若使用 `EfCore` Outbox，请确保 DB 的时钟（UTC）一致并注意索引：Outbox 应对 `(Status, AvailableAt)` 建立索引以便高效查询 Pending 消息。
- DeadLetter 存储使用 Base64 编码（当 `IMessageSerializer` 可用时），查看时可 Base64 解码再使用相同序列化器反序列化回 `DeadLetterInfo`。

## 测试建议

- 单元测试：隔离 `IOutboxStore` 的实现并 mock `ITransport`，验证 Dispatcher 在成功/失败/重试路径的逻辑。
- 集成测试：使用 SQLite in-memory provider + `InMemoryTransport` 或 在 CI 中用轻量 Kafka/RabbitMQ docker 容器进行端到端测试（拦截器写出 Outbox、Dispatcher 发送并 MarkSent/DeadLetter）。

## 未来改进建议

- 把 `TransportMetadata` 改为 `byte[]`，在数据库层以二进制存储 DeadLetter 信息，避免 Base64 并提高吞吐性能。
- 提供 provider-specific 优化（Postgres 的原生批量 UPDATE/RETURNING，SQL Server 的批量 MERGE 等）。
- 增加管理 API（查看 Outbox 状态、人工重试、DeadLetter 导出/修复工具）。

---

如果你愿意，我可以：
- 继续把 `TransportMetadata` 改成 `byte[]` 并生成 EF migration 示例；
- 添加一个简单的 xUnit 集成测试（SQLite in-memory），演示拦截器 + dispatcher 的 end-to-end 行为；
- 把 README 翻译为英文或扩展更多示例代码片段。

你想先做哪一项？
