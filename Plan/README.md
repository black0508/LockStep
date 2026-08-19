# LockStep 规划文档

本目录只放方案，不放可编译代码。工程代码以后在上级目录 `E:\Unity\Project\LockStep\` 里建。

当前约定：Unity 2022.3、3D、立方体占位、独立进程服务器、手搓 KCP+protobuf。第 1 周经典帧同步 demo，第 2 周预测回滚 + 可选 C++ 换服务器。

## 文档索引

| 文档 | 内容 |
|---|---|
| [01-architecture.md](01-architecture.md) | 分层：Transport / Protocol / Session·Room / Sim / View，依赖规则与调用链 |
| [02-protocol.md](02-protocol.md) | 包格式、`msgId`、消息字段、进房与锁帧流程（`.proto` 草案） |
| [03-gamestate.md](03-gamestate.md) | 确定性状态、移动/攻击/减速、校验和、快照与 `SimPose` |
| [04-schedule.md](04-schedule.md) | 14 天任务拆解与验收（仍是规划，不是实现步骤代码） |

## 已锁定的决策

- 服务器永远是独立进程。不用 C++ 时也是 C# 控制台，不是 Unity 主机房。
- 服务器只凑输入、广播、对比校验和，**不跑**战斗模拟。
- 网络底层是纯 C# 的 `ITransport`。KCP 内核参考 Fan_LockStep/Fantasy（ikcp），不整包引入 Fantasy Session。朋友说的三种是 KCP/TCP/WebSocket，本项目只用 KCP。
- 两周内不做通用 Buff 框架、不做角色动画、不把 Sim 用 C++ 再写一遍。
