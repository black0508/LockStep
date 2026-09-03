# 协议草案

`.proto` 是消息的唯一源。C# / C++ 都从同一份生成。源文件：[`Config/lockstep.proto`](../Config/lockstep.proto)。生成：跑 [`Tools/gen-proto.bat`](../Tools/gen-proto.bat)，会写出客户端和服务端各一份 `Lockstep.cs`。

## 传输约定

| 项 | 值 |
|---|---|
| 默认端口 | `7777` UDP |
| 传输 | kcp2k（`KcpClient` / `KcpServer`），**不是** Fantasy `Session`，也不是自写 UDP+ikcp |
| 业务包格式 | 一条 KCP 报文 = `uint16_le msgId` + 对应 protobuf 消息 |
| 粘包 | 不做（KCP 一条 `Send` 对应一条 `Recv`） |
| 字节序 | protobuf 自身 |
| protocol_version | `1`，不匹配则 `JoinReject` |

客户端 **KCP 握手完成** 后立刻发 `C2S_Join`，不要静默等。

## KCP 握手（Transport 层，不是 proto）

握手由 kcp2k 完成：可靠通道上的 `Hello` + 4 字节 cookie，外加 1 字节 channel。业务层只在 `OnConnected` / `connected` 之后发消息。

不要再实现 Fantasy 的 `RequestConnection` / `WaitConfirmConnection` / `ConfirmConnection` / `RepeatChannelId` / `ReceiveData`。那些头和 kcp2k **连不上**。

`C2S_Join` / 后续锁帧消息只出现在 kcp2k 已经鉴权、`OnData` 给出的完整报文里。Transport 禁止在握手里塞玩法字段。握手不属于 protobuf。

## 当前消息（msgId 冻结）

一条业务包：`uint16_le msgId` + 该消息的 protobuf 字节。不要再包一层 `Packet oneof`。

| msgId | 消息 | 方向 |
|---:|---|---|
| 1 | `C2S_Ping` | C → S |
| 2 | `S2C_Pong` | S → C |
| 3 | `C2S_Join` | C → S |
| 4 | `S2C_JoinAck` | S → C |
| 5 | `S2C_JoinReject` | S → C |
| 6 | `S2C_MatchStart` | S → C |

锁帧用的 `C2S_InputCmd` / `S2C_FrameInputs` / `C2S_Checksum` / `S2C_Desync` **做到那一步再写入 proto**，不要提前占位。不要把位置、血量、Transform 加成新消息——那会变成状态同步。

## 草案正文

```protobuf
syntax = "proto3";
package lockstep;
option csharp_namespace = "Lockstep.Proto";

message C2S_Ping {
  uint32 client_send_ms = 1;
}

message S2C_Pong {
  uint32 client_send_ms = 1;
}

message C2S_Join {
  uint32 protocol_version = 1;
}

message S2C_JoinAck {
  uint32 player_id = 1;   // 0 或 1，按连接分配
}

message S2C_JoinReject {
  uint32 reason = 1;      // 1=版本  2=房间满  3=已开始
}

message S2C_MatchStart {
  uint32 tick_hz = 1;           // 20
  uint32 input_delay_frames = 2; // 第1周=2
  uint32 seed = 3;              // 本 demo 可以不消费，占位
  int32 arena_half_extent_mm = 4; // 4000
}
```

## 字段规则

**playerId 以连接为准。** 以后的 `C2S_InputCmd` / `C2S_Checksum` 不带 `player_id`。服务器用 `conv → playerId` 表盖章后再写入 `S2C_FrameInputs`。

**输入是离散的。** `dx/dz` 只允许 -1/0/1，避免浮点摇杆在两端编码不一致。对角移动的归一化在 Sim 里用整数完成，见 [03-gamestate.md](03-gamestate.md)。

**不传朝向。** Yaw 由 Sim 根据位移推出来，保证两端一样。

## 进房时序

```text
C0 connect → C2S_Join → S2C_JoinAck(player_id=0)          等待
C1 connect → C2S_Join → S2C_JoinAck(player_id=1)
S  → 两人 S2C_MatchStart（相同字段）
C0/C1 从 frame 0 开始发 C2S_InputCmd（锁帧做到再加这条消息）
```

`S2C_MatchStart` 之前的输入丢弃。第三人 `C2S_Join` → `S2C_JoinReject(房间满)`。

## 锁帧时序（第 1 周）

`InputDelay = 2`，`TickHz = 20`，体感延迟约 100ms。

启动补偿：`S2C_MatchStart` 后客户端立刻把 `frame = 0 .. delay-1` 的输入发出去（通常是 0 输入），这样第 0 帧不必空等一个 RTT 之外再加 delay。

之后稳态：

```text
客户端当前「已执行确认帧」为 N-1，即将执行 N
  采样本地键位，发送 C2S_InputCmd(frame = N + delay)
  阻塞直到收到 S2C_FrameInputs(N)
  Sim.Step(N)
  发送 C2S_Checksum(N, hash)
```

服务器：

```text
收到 C2S_InputCmd(F) → slots[F][playerId] = input
当 slots[F] 两人齐 → 广播 S2C_FrameInputs(F)，释放该槽
```

第 1 周**没有**客户端预测：没收到 `S2C_FrameInputs(N)` 就不 `Step(N)`。画面可以插值已经执行过的姿态，但逻辑不抢跑。

第 2 周仍发/收同一套 `C2S_InputCmd` / `S2C_FrameInputs`；预测只发生在客户端 Session 内部。

## Checksum 与 Desync

- 每个确认帧，每个客户端发一次 `C2S_Checksum`
- 服务器按帧对比两个 hash，只在**两者都到达且不相等**时发 `S2C_Desync`
- 客户端收到 `S2C_Desync`：停逻辑、HUD 红字、打日志。两周内不做自动对账修复

## Ping

客户端每 500ms 发一次 `C2S_Ping`。RTT = 收到 `S2C_Pong` 时的本地时间 - `client_send_ms`。`client_send_ms` 用客户端单调时钟毫秒即可，服务器原样回。

## 明确不进协议的东西

- 位置、HP、buff 剩余、Transform
- 动画事件、相机、插值
- RPC、 Mirror 风格 Command
- 3 人、观战、重连追帧、录像（后续作业）
