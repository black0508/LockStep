# LockStep

基于 kcp2k 与 Protobuf 的帧同步原型。Unity 客户端做模拟和表现，.NET 服务端做房间和组帧。

当前是两人一局：到齐后自动开战，客户端用本地固定步长时钟领先服务端，提交带帧号的 WASD 方向；服务端按帧号缓存并固定频率广播全体输入。客户端收到权威帧立即执行，用整数坐标在平面上移动；尚未实现预测与回滚，移动仍需等待权威结果。对局中断线会结束本局。

领先量参考 NKGMobaBasedOnET：目标为半个 RTT 的帧数加 1 个缓冲帧，落后时立即追帧，偏离时变速收敛；RTT 复用 kcp2k 自带的 ping。缺失输入沿用上一帧方向，迟到输入丢弃。设计与 Unity 手动验收见 [帧同步方案](docs/frame-sync-plan.md)。

## 仓库

| 路径 | 说明 |
|---|---|
| `Client/LockStep` | Unity 2022.3 客户端 |
| `Server/LockStep.Server` | .NET 9 服务端 |
| `Config/lockstep.proto` | 协议 |
| `Tools` | 协议生成 |

两端各自使用 World / Entity / Component 组织逻辑，协议代码由 proto 生成，没有共享程序集。

## 后续
- 回放
- 断线重连
- 定点数
- 确定性随机数
- 失步校验
- 表现插值
- 预测与回滚
- 碰撞
- 观战
- 多房间
- 技能等离散操作
