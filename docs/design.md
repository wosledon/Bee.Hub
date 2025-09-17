# Bee.Hub

## 概述

Bee.Hub 是一个轻量、模块化的消息总线框架，旨在为 .NET 应用提供可插拔的消息传输实现、清晰的消息契约和便捷的 ASP.NET Core 集成。它支持多种传输实现（内存、Kafka、RabbitMQ），并提供适用于微服务和实时通信（SignalR）的最佳实践。

文档目标：说明总体架构、模块边界、消息模型、传输层规范、可靠性/一致性保证、扩展点、监控与测试建议，以及使用示例与路线图。

## 项目结构（模块一览）

- `Bee.Hub.Core` — 公共接口与核心抽象（总线接口、发布/订阅模型、消息处理管道、错误策略、序列化契约）。
- `Bee.Hub.Contracts` — 消息契约（DTO/事件定义、版本兼容策略、契约测试辅助）。
- `Bee.Hub.InMemory` — 供开发/测试使用的内存传输实现（轻量，非持久化）。
- `Bee.Hub.Kafka` — 基于 Kafka 的生产/消费实现，包含分区/消费组/幂等性/事务支持的说明。
- `Bee.Hub.RabbitMQ` — 基于 RabbitMQ 的实现，包含交换器/队列/路由键的约定和重试策略示例。
- `Bee.Hub.AspNetCore` — ASP.NET Core 集成扩展（服务注册、中间件、主机生命周期钩子、SignalR 扩展）。

## 设计目标与契约

- 可插拔：传输实现通过接口抽象注入，默认只引用核心及契约包。
- 简单易用：在 ASP.NET Core 中最小化样板代码，通过 DI 注册并可在控制器/服务中直接注入总线客户端。
- 可观测：内建度量和可选的日志上下文（Tracing）集成点。
- 可测试：提供内存实现和契约测试工具以保证生产环境和测试环境的一致性。

## 核心抽象（Contract）

核心类型示例（概念）：

- IHubClient：发布/发送/请求/订阅的高层 API。
- IMessage：最小消息界面（Headers: IDictionary<string,string>，Payload: byte[] 或 强类型泛型）。
- ISubscriber<TMessage>：消息处理器接口，用户实现以接收消息。
- IMessageSerializer：消息序列化/反序列化抽象（JSON, MessagePack 等可插拔）。
- ITransport：低层传输接口，负责实际的发送/接收、确认、重试与连接管理。

契约规则：

- 所有事件/命令应有版本号（例如：UserCreated:v1）。
- 消息头保留字段：`bh-message-id`, `bh-correlation-id`, `bh-origin`, `bh-version`, `bh-timestamp`。
- 消息应尽量使用不可变 DTO，便于并发与序列化。

## 传输实现细节

以下为不同传输的设计考虑与实现要点：

1) InMemory（开发 / 单体测试）
- 使用内存队列与订阅分发，适用于单进程环境。
- 不保证跨进程持久性，也不保证幂等性。

2) Kafka（高吞吐、可扩展）
- 每个消息类型映射到 topic（或使用通用 topic + key 区分）；消费使用消费组实现横向扩展。
- 建议使用幂等生产者或事务（如果需要严格一次语义）。
- 消费位点由 Kafka 管理；重试策略应结合死信 topic（DLQ）与补偿逻辑。

3) RabbitMQ（灵活路由、低延迟）
- 使用 exchange+queue 的路由模型，按消息类型或路由键进行绑定。
- 支持不同的确认策略（自动 ack / 手动 ack），并在失败时推送到 DLQ。

实现注意点（所有传输通用）：
- 连接管理与重连策略（指数退避、抖动）。
- 心跳与连接健康探测。
- 可配置的批量发送/批量确认以优化吞吐。

## 可靠性与一致性策略

- at-most-once、at-least-once 与 exactly-once 的文档说明，以及默认策略（通常为 at-least-once）。
- 幂等性：建议消息携带全局唯一 ID（bh-message-id），处理端根据该 ID 去重（可选：在共享存储或外部去重缓存）。
- 重试策略：可配置的指数退避、最大重试次数、与最终 DLQ 路由。
- 事务/补偿：在需要跨多个系统一致性时，推荐使用 SAGA/补偿模式或 Kafka 事务（当底层支持时）。

## 可观测性（监控与日志）

- 计量：发布/消费计数、处理延迟分布、处理失败计数、队列/主题滞留深度。
- 日志：每条消息记录关键头信息（message-id, correlation-id, handler, duration, error）并支持 structured logging。
- 分布式跟踪：支持将 TraceId/SpanId 注入消息 headers，以便与 OpenTelemetry 集成。

## 安全

- 传输层安全：支持 TLS/SSL 配置（Kafka/RabbitMQ 连接加密）。
- 认证/授权：对于网关或管理 API 提供可插拔的认证点（基于证书、用户名密码或 OAuth token）。
- 敏感数据：建议在消息发送前对敏感字段进行加密或脱敏，并在契约中标注敏感字段。

## 扩展点（插件/中间件）

- 中间件管道：在消息发送/接收路径上支持中间件（类似 ASP.NET Core 的管道），可插入审计、计量、重试、幂等校验等功能。
- 自定义序列化器：实现 `IMessageSerializer` 即可切换 JSON、MessagePack 或自定义格式。
- 自定义路由/策略：传输实现可暴露策略接口来控制路由、分区键或 DLQ 策略。

## ASP.NET Core 集成

主要功能：

- 在 `Startup`/Program.cs 中通过扩展方法快速注册：AddBeeHub(options => { options.UseKafka(...); });
- 支持将 `IHubClient` 注入到控制器、HostedService 中。
- 提供 HostedService，用于在主机启动时初始化连接，并优雅关闭时清理资源。
- SignalR 集成：提供 `UseBeeHubSignalR()` 扩展，可将消息事件转发到已连接的 WebSocket 客户端（或者将 SignalR 事件发布到消息总线）。

示例（伪代码）：

// Program.cs
// ...existing code...
// services.AddBeeHub(b => b.UseInMemory());

更多示例请参见 `Bee.Hub.AspNetCore` 文档与代码样例（在后续 TODO 中补充完整示例片段）。

## 测试策略

- 单元测试：核心抽象与中间件管道的行为测试（同步/异步处理、异常传播、头透传）。
- 集成测试：使用 `Bee.Hub.InMemory` 或测试容器（Testcontainers）运行 Kafka/RabbitMQ 的真实集成测试。
- 契约测试：对 `Bee.Hub.Contracts` 使用契约测试（Pact 或自定义）以保证服务之间的兼容性。
- 端到端（E2E）：在 CI 中使用 ephemeral broker（例如 Docker Compose 或 Testcontainers）跑关键场景。

推荐 CI 流程：

- 静态分析（StyleCop / Roslyn 分析）。
- 单元测试 -> 覆盖率门槛。
- 集成测试（对选定模块）在 nightly 或 PR gated 环境运行。

## 使用示例（高层伪代码）

发布事件：

// var client = provider.GetRequiredService<IHubClient>();
// await client.PublishAsync(new UserCreated { Id = 123, Email = "x@e.com" }, headers: new { correlationId = "..." });

订阅/处理：

// class UserCreatedHandler : ISubscriber<UserCreated> {
//   Task Handle(UserCreated msg, MessageContext ctx) { /* 处理并返回 */ }
// }

SignalR 转发（伪）：

// 在消息处理管道中加入中间件，将事件广播到指定 SignalR 群组。

## Bee.Hub.EfCore

目标：提供一个基于 Entity Framework Core 的持久化扩展，用于将消息持久化到关系型数据库，支持 Outbox/Inbox 模式以保证跨服务事务一致性，并为消息查询、审计和补偿流程提供一套通用的数据模型与查询 API。

主要职责：
- 持久化发布事件（Outbox）以保证在事务提交时可靠地把消息写入本地数据库并在稍后异步投递到传输层。
- 接收端的去重（Inbox）与幂等处理支持，避免重复消费。
- 提供按属性/时间/状态查询消息的能力（用于审计、补偿、重发或手动处理）。
- 管理消息的送达状态、重试元数据与死信（DLQ）标记。

数据模型建议：
- OutboxMessage
	- Id (GUID, PK)
	- MessageType (string)
	- Payload (byte[] 或 string)
	- Headers (JSON 文本)
	- CreatedAt (UTC datetime)
	- AvailableAt (UTC datetime) — 支持延迟投递
	- AttemptCount (int)
	- LastAttemptAt (UTC datetime?)
	- Status (enum: Pending, Sent, Failed, DeadLetter)
	- TransportMetadata (JSON 文本) — 可选，保存传输相关字段

- InboxMessage
	- Id (GUID, PK) — 可选：或以消息原始 ID 为主键
	- MessageId (string) — 源消息 ID（bh-message-id）
	- MessageType (string)
	- ReceivedAt (UTC datetime)
	- ProcessedAt (UTC datetime?)
	- Handler (string?) — 处理该消息的处理器标识
	- Status (enum: Received, Processed, Failed)

设计要点：
- Outbox Pattern：在处理应用程序本地业务事务时，将要发布的消息先保存到 Outbox 表中（与业务数据同一事务），然后运行一个后台投递器（BackgroundDispatcher）异步读取 Pending 状态的 OutboxMessage，调用 ITransport.PublishAsync 发布到目标传输，并在成功后将状态标记为 Sent（或删除记录，视策略而定）。
- Inbox 去重：在消费者端处理消息前，先写入 Inbox 表（或者先检测是否存在同一 MessageId 的 Processed 记录），以保证幂等性。若检测到已处理，则跳过处理逻辑。
- 事务一致性：推荐将 Outbox 写入与业务写入放在同一数据库事务内，确保原子性。对于分布式事务，优先考虑补偿/SAGA 模式而非强一致分布式事务。
- 重试与 DLQ：OutboxMessage 保存 AttemptCount 与 LastAttemptAt，投递器在调用失败时按重试策略（指数退避）重试，超过阈值将标记为 Failed/DeadLetter 并记录错误元信息以供人工干预或补偿。
- 可配置序列化：Payload 与 Headers 支持自定义序列化器（JSON / MessagePack / 自定义），在数据库中可选择以 JSON 文本或二进制 blob 存储。

查询与索引：
- 为高频查询字段建索引：MessageType、Status、AvailableAt、CreatedAt、MessageId。
- 提供分页查询 API（基于 CreatedAt 或 Id 的游标分页）以便高效导出或批量重发。
- 支持按时间范围、消息类型、状态筛选并返回聚合信息（例如失败计数、滞留深度）。

迁移与维护：
- EF Core Migrations：提供示例迁移脚本并在 README 中说明如何在生产环境逐步应用迁移（备份、维护窗口、并发写入考虑）。
- 清理策略：提供可配置的归档/清理作业，用于删除或归档已 Sent/Processed 很久的消息（按保留期策略）。

性能与调优：
- 批量读取：投递器应按批次读取 Pending OutboxMessage 并并行投递以提高吞吐。注意并发度以避免数据库与传输端压力。
- 连接与事务开销：尽量重用 DbContext/连接池并在投递批次中控制事务大小。
- 表分区：当消息量非常大时，建议使用表分区或按时间/类型分表以减小单表索引压力。

扩展点：
- 自定义存储：尽管默认以 EF Core 为实现，但接口应允许替换为其他持久化实现（例如直接使用 Dapper、或写入事件存储系统）。
- 可插拔序列化器：实现 `IMessageSerializer` 并在 `EfCore` 模块中注入使用。
- 事件追踪：提供审计日志扩展点以接入 OpenTelemetry/LogSink。

示例代码片段（EF Core 实体与 DbContext，示意）：

```csharp
public class OutboxMessage
{
		public Guid Id { get; set; }
		public string MessageType { get; set; } = null!;
		public string Payload { get; set; } = null!; // 可为 JSON
		public string Headers { get; set; } = null!; // JSON
		public DateTime CreatedAt { get; set; }
		public DateTime AvailableAt { get; set; }
		public int AttemptCount { get; set; }
		public DateTime? LastAttemptAt { get; set; }
		public string Status { get; set; } = "Pending";
}

public class BeeHubDbContext : DbContext
{
		public DbSet<OutboxMessage> Outbox { get; set; } = null!;
		public DbSet<InboxMessage> Inbox { get; set; } = null!;

		public BeeHubDbContext(DbContextOptions<BeeHubDbContext> options) : base(options) { }

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
				modelBuilder.Entity<OutboxMessage>().HasKey(x => x.Id);
				modelBuilder.Entity<OutboxMessage>().HasIndex(x => new { x.Status, x.AvailableAt });
				// ...其他映射
		}
}
```

后台投递器（概要）：

- BackgroundOutboxDispatcher 每隔 N 秒拉取一批 Pending 的 OutboxMessage（且 AvailableAt <= now），对每条消息调用注入的 `IHubClient.PublishAsync` 或 `ITransport.PublishAsync`，并根据结果更新 AttemptCount/Status/LastAttemptAt。

测试建议：
- 单元测试：测试 Outbox 写入逻辑、序列化器插拔、Inbox 去重判断。
- 集成测试：使用内存或轻量级数据库（SQLite）测试事务一致性与投递流程；使用 Docker 容器结合真实传输（Kafka/RabbitMQ）验证端到端行为。

文档：在 `Bee.Hub.EfCore` 章末添加示例配置与迁移命令，以及常见故障排查（例如锁竞争、长事务导致的投递延迟）。

## 路线图

- v0.1 — 核心抽象、内存实现、ASP.NET Core 基本集成。
- v0.2 — Kafka 与 RabbitMQ 实现、序列化器插件、DLQ 支持。
- v0.3 — OpenTelemetry 集成、契约测试工具、更多示例与模板。
- v1.0 — 稳定 API、性能优化、官方 nuget 包发布与示例应用模板。

## 开发与贡献指南（简要）

- 代码风格：遵循 .NET 团队常见实践（nullable enabled, async suffix, XML docs for public API）。
- 分支策略：主干 `main` 保持可发布状态，feature 分支以 `feature/` 前缀，发布使用 `release/`。
- 提交信息：使用简短描述 + 关联 Issue（如有）。

---

如果你想，我可以：

- 1) 继续把 `docs/` 下增加更详细的使用示例和 API 参考（将执行 TODO #2）。
- 2) 为 `Bee.Hub.Core` 撰写 README 示例并添加一个小的集成测试项目（将执行 TODO #3）。

请告诉我你希望接着做哪一项，或让我继续按照 TODO 列表完成下一步。