# 协议草案

`.proto` 是消息的唯一源。C# / C++ 都从同一份生成。下面是草案，实现阶段再放到 `proto/lockstep.proto`。

## 传输约定

| 项 | 值 |
|---|---|
| 默认端口 | `7777` UDP |
| 传输 | Fantasy 同款 KCP 内核（ikcp）+ 自写极简握手，**不是** Fantasy `Session` |
| 业务包格式 | `uint16` 小端 `msgId` + protobuf 体 |
| 粘包 | 不做（KCP 一条 `Send` 对应一条 `Recv`） |
| 字节序 | protobuf 自身；`msgId` 小端 |
| protocol_version | `1`，不匹配则 `JoinReject` |

客户端 **KCP 握手完成** 后立刻发 `Join`，不要静默等。

## KCP 握手（Transport 层，不是 proto）

Fan_LockStep 的 Fantasy KCP 在 ikcp 外面还有 5 字节保留头 + 连接状态机。我们第 1 周 C#、第 2 周 C++ 必须用**同一套**，否则只「参考了 KCP 内核」仍连不上。

规划采用 Fantasy 的 `KcpHeader` 语义，自己实现，不依赖 Fantasy 程序集：

| 值 | 含义 |
|---|---|
| `0x01 RequestConnection` | 客户端 → 服务器，申请 conv/channel |
| `0x02 WaitConfirmConnection` | 服务器 → 客户端，带回分配的 conv |
| `0x03 ConfirmConnection` | 客户端 → 服务器，确认；此后才走 ikcp 数据 |
| `0x04 RepeatChannelId` | conv 冲突，客户端换一个再申请 |
| `0x06 ReceiveData` | 握手后的 KCP 数据报 |
| `0x07 Disconnect` | 任一侧断开 |

`Join` / `InputCmd` 等只出现在 `ReceiveData` 解开 ikcp 之后。Transport 禁止在握手包里塞玩法字段。握手不属于 protobuf，见下方「KCP 握手」。

## msgId

| id | 消息 | 方向 |
|---:|---|---|
| 1 | `Ping` | C → S |
| 2 | `Pong` | S → C |
| 3 | `Join` | C → S |
| 4 | `JoinAck` | S → C |
| 5 | `JoinReject` | S → C |
| 6 | `MatchStart` | S → C |
| 7 | `InputCmd` | C → S |
| 8 | `FrameInputs` | S → C |
| 9 | `Checksum` | C → S |
| 10 | `Desync` | S → C |

预留 11+。不要把位置、血量、Transform 加成新消息——那会变成状态同步。

## 草案正文

```protobuf
syntax = "proto3";
package lockstep;

message Ping {
  uint32 client_send_ms = 1;
}

message Pong {
  uint32 client_send_ms = 1;
}

message Join {
  uint32 protocol_version = 1;
}

message JoinAck {
  uint32 player_id = 1;   // 0 或 1，按连接分配
}

message JoinReject {
  uint32 reason = 1;      // 1=版本  2=房间满  3=已开始
}

message MatchStart {
  uint32 tick_hz = 1;           // 20
  uint32 input_delay_frames = 2; // 第1周=2
  uint32 seed = 3;              // 本 demo 可以不消费，占位
  int32 arena_half_extent_mm = 4; // 4000
}

message InputCmd {
  uint32 frame = 1;
  sint32 dx = 2;        // -1 / 0 / 1 ，世界 X
  sint32 dz = 3;        // -1 / 0 / 1 ，世界 Z（前进）
  bool attack = 4;
}

message PlayerInput {
  uint32 player_id = 1;
  sint32 dx = 2;
  sint32 dz = 3;
  bool attack = 4;
}

message FrameInputs {
  uint32 frame = 1;
  repeated PlayerInput inputs = 2; // 恰好 2 条，按 player_id 升序
}

message Checksum {
  uint32 frame = 1;
  uint64 hash = 2;
}

message Desync {
  uint32 frame = 1;
  uint64 hash_player0 = 2;
  uint64 hash_player1 = 3;
}
```

## 字段规则

**playerId 以连接为准。** `InputCmd` / `Checksum` 不带 `player_id`。服务器用 `conv → playerId` 表盖章后再写入 `FrameInputs`。

**输入是离散的。** `dx/dz` 只允许 -1/0/1，避免浮点摇杆在两端编码不一致。对角移动的归一化在 Sim 里用整数完成，见 [03-gamestate.md](03-gamestate.md)。

**不传朝向。** Yaw 由 Sim 根据位移推出来，保证两端一样。

## 进房时序

```text
C0 connect → Join → JoinAck(player_id=0)          等待
C1 connect → Join → JoinAck(player_id=1)
S  → 两人 MatchStart（相同字段）
C0/C1 从 frame 0 开始发 InputCmd
```

`MatchStart` 之前的 `InputCmd` 丢弃。第三人 `Join` → `JoinReject(房间满)`。

## 锁帧时序（第 1 周）

`InputDelay = 2`，`TickHz = 20`，体感延迟约 100ms。

启动补偿：`MatchStart` 后客户端立刻把 `frame = 0 .. delay-1` 的输入发出去（通常是 0 输入），这样第 0 帧不必空等一个 RTT 之外再加 delay。

之后稳态：

```text
客户端当前「已执行确认帧」为 N-1，即将执行 N
  采样本地键位，发送 InputCmd(frame = N + delay)
  阻塞直到收到 FrameInputs(N)
  Sim.Step(N)
  发送 Checksum(N, hash)
```

服务器：

```text
收到 InputCmd(F) → slots[F][playerId] = input
当 slots[F] 两人齐 → 广播 FrameInputs(F)，释放该槽
```

第 1 周**没有**客户端预测：没收到 `FrameInputs(N)` 就不 `Step(N)`。画面可以插值已经执行过的姿态，但逻辑不抢跑。

第 2 周仍发/收同一套 `InputCmd` / `FrameInputs`；预测只发生在客户端 Session 内部。

## Checksum 与 Desync

- 每个确认帧，每个客户端发一次 `Checksum`
- 服务器按帧对比两个 hash，只在**两者都到达且不相等**时发 `Desync`
- 客户端收到 `Desync`：停逻辑、HUD 红字、打日志。两周内不做自动对账修复

## Ping

客户端每 500ms 发一次 `Ping`。RTT = 收到 `Pong` 时的本地时间 - `client_send_ms`。`client_send_ms` 用客户端单调时钟毫秒即可，服务器原样回。

## 明确不进协议的东西

- 位置、HP、buff 剩余、Transform
- 动画事件、相机、插值
- RPC、 Mirror 风格 Command
- 3 人、观战、重连追帧、录像（后续作业）
