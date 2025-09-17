# Bee.Hub — 设计文档（概览）

本文件为 Bee.Hub 的设计参考，概述核心抽象、模块边界、可靠性策略、EF Core Outbox/Inbox 模式、以及在 ASP.NET Core 中的集成方式与示例。

## 一句话说明

Bee.Hub 是一个可插拔的 .NET 消息总线框架：定义清晰的核心契约（序列化器、传输、订阅器），并提供 InMemory、Kafka、RabbitMQ 与 EF Core Outbox 的参考实现与集成点。

## 目录

- 核心目标与设计原则
- 项目/模块一览
- 核心抽象与契约
- 传输实现要点（InMemory / Kafka / RabbitMQ）
- Bee.Hub.EfCore（Outbox / Inbox）
- ASP.NET Core 集成示例
- 可观测性、监控与测试策略
- 迁移、性能与运维建议

## 核心目标与设计原则

- 可插拔：通过接口替换传输与序列化实现。
- 事务一致性：支持 Outbox 模式以保证业务写入与消息持久化的原子性。
- 简洁性：在 ASP.NET Core 中尽量少的样板代码与易懂的 DI 注册。
- 可观测：支持结构化日志、计量与可选的 Trace 上下文透传。

## 模块（快速一览）

- `Bee.Hub.Core`：核心抽象（`ITransport`, `IMessageSerializer`, `IHubClient`, `ISubscriber<T>`）。
- `Bee.Hub.InMemory`：开发/测试用，非持久化。基于 `System.Threading.Channels`。
- `Bee.Hub.Kafka`：Kafka 参考实现（Confluent.Kafka）。
- `Bee.Hub.RabbitMQ`：RabbitMQ 参考实现（RabbitMQ.Client）。
- `Bee.Hub.AspNetCore`：中间件、HostedService、SignalR 桥接示例。
- `Bee.Hub.EfCore`：Outbox/Inbox、SaveChanges 拦截器、后台 Dispatcher、`IOutboxStore`/`IInboxStore`。

## 核心抽象与契约

- IMessageSerializer
	- byte[] Serialize<T>(T obj)
	- T Deserialize<T>(byte[] data)
- ITransport
	- Task PublishAsync(string messageType, byte[] payload)
	- （实现可包含确认、事务、批量等能力）
- IOutboxStore / IInboxStore
	- 抽象持久化操作（AddToContext、GetPendingBatchAsync、MarkSentBatchAsync、IncrementAttemptBatchAsync、MarkDeadLetterBatchAsync 等）

契约要点：
- 消息必须携带唯一 ID（bh-message-id）与 version 字段。
- Message headers 使用 JSON 存储在 Outbox 的 Headers 字段。

## 传输实现要点

- InMemory：轻量，适合单进程测试。无持久化/不保证跨进程可靠性。
- Kafka：高吞吐，建议使用幂等生产者或事务用于 exactly-once（若需要）。使用 topic/partition 策略映射消息类型。
- RabbitMQ：灵活路由，支持 exchange/queue/路由键和 DLQ。

所有传输应实现：连接管理、重试与回退、批量/并发控制与必要的指标埋点。

## Bee.Hub.EfCore（Outbox / Inbox）

目标：保证“业务数据写入 + 消息入库（Outbox）”在同一事务内完成，由后台 Dispatcher 异步负责可靠投递。

数据模型（摘要）：

- OutboxMessage
	- Id (GUID)
	- MessageType (string)
	- Payload (byte[])
	- Headers (JSON string)
	- CreatedAt (UTC)
	- AvailableAt (UTC)
	- AttemptCount (int)
	- LastAttemptAt (UTC?)
	- Status (Pending / Sent / DeadLetter)
	- TransportMetadata (string or byte[]) — 存储 DeadLetterInfo 等元数据

- InboxMessage
	- MessageId (string)
	- MessageType (string)
	- ReceivedAt, ProcessedAt, Handler, Status

关键组件：
- OutboxSaveChangesInterceptor
	- 在 `SaveChanges` 阶段扫描实体的 domain events，并把事件序列化为 `OutboxMessage`，通过 `AddToContext` 写入当前 DbContext（事务内）。
- BackgroundOutboxDispatcher
	- 周期性拉取 Pending 的 OutboxMessage（按 AvailableAt、批量），调用 `ITransport.PublishAsync` 发送。
	- 对成功消息使用 `MarkSentBatchAsync` 批量标记为 Sent。
	- 对失败消息收集并执行 `IncrementAttemptBatchAsync`，若超过阈值则使用 `MarkDeadLetterBatchAsync` 写入 `DeadLetterInfo`（序列化后保存）。

序列化与 TransportMetadata：
- Payload 使用 `IMessageSerializer`（返回 `byte[]`），Dispatcher 直接发送此二进制数据。
- DeadLetterInfo（包含 reason/exception/stack/occurredAt）会被序列化并保存在 `TransportMetadata`。
	- 若使用 `IMessageSerializer`，Dispatcher 会把序列化结果做 Base64 存为字符串（当前兼容方案）；或可将字段改为 `byte[]` 来直接保存二进制。

索引建议：
- (Status, AvailableAt)
- MessageType
- CreatedAt

性能与可扩展性建议：
- Dispatcher 批量读取并并行发送，提高吞吐；但控制并发以免压垮传输或 DB。
- 在高写入场景下考虑分区表或按时间分表策略。
- 使用数据库原生批量更新（ExecuteSqlRaw）可显著减少 SaveChanges 开销（留作优化）。

## ASP.NET Core 集成（示例）

推荐的 DI 注册顺序：

```csharp
services.AddSingleton<IMessageSerializer, DefaultJsonSerializer>();
services.AddSingleton<ITransport, InMemoryTransport>(); // 或其他实现

// EfCore
services.AddScoped<OutboxSaveChangesInterceptor>();
services.AddDbContext<BeeHubDbContext>((sp, opt) => {
		opt.UseSqlite("Data Source=beehub.db");
		opt.AddInterceptors(sp.GetRequiredService<OutboxSaveChangesInterceptor>());
});
services.AddBeeHubEfCore(); // 注册 stores + dispatcher

// AspNetCore extras
services.AddBeeHubAspNetCore(); // signalR bridge / middleware
```

使用说明：
- 域事件写入：在实体上累积 domain events，然后调用 `dbContext.SaveChanges()`。拦截器会把事件转换为 OutboxMessage 并在事务提交时写入。
- 不要在事务内直接调用 `ITransport.PublishAsync`（会导致事务不一致）。

## 可观测性、监控与测试

- 建议埋点：publish/consume 计数、处理耗时、失败计数、Outbox 滞留深度。
- DeadLetter 导出与告警（当 DeadLetter 数量异常增长时触发告警）。
- 测试：使用 InMemory / SQLite-in-memory 进行单元与集成测试；在 CI 中使用 Testcontainers 运行 Kafka/RabbitMQ 做端到端验证。

## 迁移与运维注意

- EF Core migration：示例迁移脚本应包含 TransportMetadata 列的类型变更说明（string -> byte[]）以支持二进制存储。
- 清理策略：提供可配置的保留期（例如 30/90 天）并实现归档或删除任务以保持表规模可控。
- 维护窗口：在大规模迁移或索引重建时安排窗口并监控写入延迟。

## 常见问题与建议

- 是否应该把 TransportMetadata 改为 byte[]？
	- 推荐在高吞吐与严格存储效率场景改为 `byte[]`，但会需要 DB migration。当前实现兼顾可读性（文本 JSON）与兼容性（Base64）。

- 如何实现幂等性？
	- 使用 `bh-message-id` + Inbox 表做去重，或在处理端使用外部幂等存储（Redis 等）。
