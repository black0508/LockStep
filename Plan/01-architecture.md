# 分层架构

目标：网络作为长期底层留下去；玩法、锁帧、画面都可以换，但不倒灌进 Transport。

## 依赖方向

只允许向下依赖。下层禁止 `using` 上层类型。

```text
View        → Sim, Session（只读姿态 / 本地输入）
Sim         → 无网络、无 UnityEngine
Session     → Protocol, Transport, Sim
Room        → Protocol, Transport          （服务端，无 Sim）
Protocol    → 无 Transport 实现细节
Transport   → 无 protobuf、无帧号、无玩家
NetworkManager → Tick Transport + 组装 Session （Unity 薄适配）
```

「网络组件」指 `ITransport` + `KcpTransport` + `MsgCodec` 这一组纯 C# 类，两端共用。不是挂在物体上的万能 `MonoBehaviour`。

## 两端怎么组装

```text
Unity 客户端进程
  NetworkManager   每渲染帧 Tick 传输，组装 Session
  KcpTransport     连 127.0.0.1:7777
  MsgCodec         msgId + protobuf
  LockstepSession  Join、按帧发输入、收 FrameInputs
  Simulation       推进 GameState、算 hash
  CubePlayerView   把 SimPose 画成立方体
  HudView          frame / delay / hash / rtt

C# 控制台服务器进程（第 2 周可整进程换成 C++）
  NetworkServer       组装 Transport、每轮 Tick
  KcpServerTransport  听 UDP
  MsgCodec            同一套
  Room                2 人、按帧凑齐输入、广播、对 checksum
```

C++ 服务器按同样三层切目录：`transport/`、`protocol/`、`room/`，语义对齐，不移植 Sim。

## 各层契约

### 1. Transport

只处理连接与字节。KCP 的 conv、窗口、重传都停在这一层。

```text
ITransport
  Tick(nowMs)
  Send(payload)
  TryRecv(out payload) -> bool
  IsConnected
  事件：Connected / Disconnected   （实现时再定，规划里保留）

IServerTransport
  Tick(nowMs)
  TryAccept(out conv, out ITransport peer) -> bool
  Bind(port)
```

允许以后加装饰实现，而不改 Session：

- `LagTransport`：人工延迟，测 lockstep / rollback
- `LoopbackTransport`：单进程测 Codec（实现阶段再用）

KCP 实现选定 **kcp2k**（Mirror 官方传输封装）。两端都用同一份 `KcpClient` / `KcpServer`，不要再手写 UDP + ikcp + Fantasy 握手。朋友说的「三种」是 Fantasy 的传输选项 **KCP / TCP / WebSocket**，不是三种 KCP 算法。我们只用 KCP。

不要整包引入 Mirror。不要把 `Scene.Connect`、Fantasy `Session`、Opcode、RPC、Entity 搬进来。

外面仍只暴露现有 Transport 外观：`Connect` / `Tick` / `Send` / `TryRecv` / `IsConnected`。第 2 周若换 C++ 服务器，必须说 **kcp2k 线协议**（channel + cookie + Hello），不能再按 Fantasy 5 字节头实现。

KCP 参数：`DualMode=false`（纯 IPv4）、`NoDelay=true`、`Interval=10`、`FastResend=2`、`CongestionWindow=false`，MTU 与窗口用 kcp2k 默认值。业务消息走 `KcpChannel.Reliable`，两周不做多通道。

**两端的 `KcpConfig` 必须逐字一致。** MTU 不一致会让大报文在收端直接抛 `SocketException`（收包缓冲区就是按 `config.Mtu` 开的），不是「慢一点」而是连不通。

**禁止**：在 Transport 里解析 `InputCmd`、记录 `playerId`、按帧号排队。

### 2. Protocol

把「一条 KCP 报文」变成「一条消息」。KCP 已是报文边界，**不做 TCP 粘包**。

```text
packet = uint16_le msgId + protobuf_bytes
```

`MsgCodec` 只认识 `msgId` 和生成出来的 protobuf 类型。房间满员、锁帧、血量都不在这一层。

消息清单与字段见 [02-protocol.md](02-protocol.md)。

### 3. Session（客户端）/ Room（服务端）

这是锁帧玩法的「会话层」，仍然不碰 Cube。

**LockstepSession**

- 连上后发 `Join`
- 收到 `MatchStart` 后进入锁帧
- 每个逻辑帧采样本地输入，打上 `frame = 当前待执行帧 + InputDelay`
- 收到 `FrameInputs(N)` 后交给 Sim 执行第 N 帧
- Sim 执行完上报 `Checksum`
- 维护 RTT（Ping/Pong）

**Room**

- 一个房间两人，按连接分配 `playerId = 0 / 1`
- 忽略客户端自报的 playerId，以连接为准
- 某帧两人 `InputCmd` 都到齐 → 广播一条 `FrameInputs`
- 两人该帧 `Checksum` 都到齐且不一致 → 广播 `Desync`
- 第 1 周：某帧输入一直不齐就等待（可加超时踢人，非 demo 必做）

**禁止**：Session/Room 里改血量、算碰撞、挪坐标。

### 4. Sim（仅客户端）

纯 C# 状态机。输入是「这一帧两名玩家的 Input」，输出是新的 `GameState` 和 `hash`。字段与规则见 [03-gamestate.md](03-gamestate.md)。

第 1 周：只在收到确认的 `FrameInputs` 时 `Step`。  
第 2 周：本地预测 `Step`，确认后可能回滚再 `Step`。Sim 的 `Step` 本身不变。

**禁止**：`UnityEngine`、`Time.deltaTime`、`Rigidbody`、`Random`（非确定性）。

### 5. View（仅客户端）

只读 `SimPose`。前期 `CubePlayerView`：位移插值、Yaw、攻击闪色、减速变灰。后期换带动画的 View，订阅同一套姿态。

Unity 里唯一允许碰 Transport 的脚本是 `NetworkManager`。

## 调用链（第 1 周 lockstep）

```text
渲染帧
  NetworkManager.Tick
    Transport.Tick → OnReceivedpacket
      Codec.Decode
        Session.OnMessage
          MatchStart → 重置 Sim
          FrameInputs(N) → Sim.Step(N, inputs) → 发 Checksum
          Pong → 更新 RTT

  Session.MaybeSampleInput   （按逻辑时钟，不是每渲染帧瞎发）
    Codec.Encode(InputCmd)
      Transport.Send

  View.Render(Sim.CurrentPose, 插值)
```

服务器：

```text
循环 Tick
  Accept 新连接
  Recv → Decode
    Join → 分配 playerId，满员则 MatchStart
    InputCmd(frame,F) → 填进帧槽，两人齐则广播 FrameInputs
    Checksum → 对比，不一致则 Desync
```

## 第 2 周回滚时层还怎么分

回滚加在 **Session + Sim**，不改 Transport/Protocol 消息集（仍收 `FrameInputs` 作为确认输入）。

- Sim：增加 `Clone()` / `Restore(snapshot)`
- Session：未确认的远程输入用「上一帧输入」预测；确认后若不一致则 Restore 到分歧帧再重演

服务器协议可以不动。这是分层的验收标准之一。

## 仓库里这些层落在哪（实现阶段再建）

```text
LockStep/
  proto/lockstep.proto          Protocol 的唯一源
  shared/Net/                   ITransport, KcpTransport, MsgCodec
  server-csharp/Room/           只引用 shared/Net
  server-cpp/                   transport / protocol / room
  client-unity/
    Assets/Lockstep.Shared/     链接 shared/Net
    Assets/Session/
    Assets/Sim/
    Assets/View/
    Assets/NetworkManager/
```

本规划阶段不创建上述工程文件。
