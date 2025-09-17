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