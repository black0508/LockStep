# LockStep

基于 kcp2k 与 Protobuf 的帧同步原型。Unity 客户端做模拟和表现，.NET 服务端做房间和组帧。

当前是两人一局：到齐后自动开战，服务端按固定频率广播全体输入，两端执行同一份权威帧，用整数坐标在平面上移动。对局中断线会结束本局。

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
