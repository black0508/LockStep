# 第一阶段帧同步：WASD 角色移动

状态：第一阶段已实现，并完成一轮整洁重构（逻辑/输入/表现分层、开战消息携带参战名单、删除冗余防御）；等待用户 Unity 手动验收。

## 目标与已确认约束

两名玩家进入现有单房间并自动开战后，各自使用 WASD 控制自己的角色在 XZ 平面移动；两个客户端执行同一份服务端帧输入，在相同逻辑帧得到相同整数坐标。

- 服务端定时封帧，使用每名玩家最近收到的有效方向；没有新消息时沿用，不等待所有人输入，也不自动将方向归零。
- 客户端上传当前移动方向，不指定目标帧号，不配置额外输入延迟帧数。
- 角色位置与移动计算采用整数，Unity 展示时才转换为浮点。
- 沿用现有 EC、KCP 可靠通道、Google.Protobuf、反射 Handler 和单房间生命周期。
- 预测与回滚是后续明确要做的阶段目标，第一阶段只实现权威帧驱动的移动；不预写预测、快照或回滚脚手架。
- 第一阶段不做断线重连、历史补帧、录像、碰撞、寻路、动画或渲染插值。
- Unity 测试由用户执行，不创建测试工程、测试脚手架或额外测试场景。
- 按根目录 AGENTS.md 执行；本文件同时保存设计和实施步骤，避免重复文档。

## 参考项目中采用的机制

参考路径：D:/Download/38.服务器帧同步逻辑代码。

参考服务端 FSManageComponentSystem 的固定周期组帧、广播，以及 FSComponentSystem 的玩家 ID 排序。目标帧输入缓存、同玩家同目标帧去重不采用。

参考项目使用 Fantasy，并包含确认帧、历史补帧、修正帧与点击目标位置协议。本阶段只采用组帧机制；WASD、现有 EC 生命周期和 KCP 消息链路以本项目为准。参考项目的客户端尚未接齐帧执行与移动。

借鉴《守望先锋》GDC 演讲中缺失输入时沿用最近输入的思路；本阶段简化为服务端保存最新方向，不实现其命令帧缓冲、客户端时间调节和预测校正体系。背景资料：[GDC 原演讲](https://gdcvault.com/play/1024001/-Overwatch-Gameplay-Architecture-and)、[演讲转录译文的网络部分](https://www.sohu.com/a/148848770_466876)。

不以本项目以前的帧同步参数作为需求依据；现有 EC、网络和房间结构继续沿用。旧 InputDelayFrames 及其协议、日志引用纳入本次移除范围。

## 帧号与输入规则

1. 每局从帧 1 开始。服务器 CurrentFrame 表示下一个待生成并广播的帧。客户端 LastAppliedFrame 表示已经执行完的帧，初始为 0。只有服务端决定输入实际用于哪些帧。
2. 服务器保留现有默认 30 Hz，通过 OnUpdate 的时间累积器按 1.0 / TickRate 封帧，不把现有 World.Update 改成固定帧。主循环仍负责驱动网络和组件。
3. 开战时每名玩家的方向初始化为 X=0、Z=0。收到有效 C2SInput 后替换对应玩家的方向；一帧内收到多条时，以封帧前最后收到的方向为准。
4. 每帧按 PlayerId 升序，将全体参战玩家当前方向复制成独立帧消息并广播，然后推进帧号。广播后保留方向，供下一帧沿用；已生成的帧不引用可被后续输入修改的对象。
5. 输入消息没有目标帧，因此不做“历史输入/未来输入”判定。晚到的消息从后续尚未封闭的帧生效，不修改已经广播的帧。服务端只保存每名玩家一个最新输入状态，不缓存未来帧或历史帧。
6. 客户端只执行连续的服务端帧；重复或已经执行的帧忽略。应用层出现帧号跳跃时记录错误并断开，结束本局，不猜测缺失帧。正常传输中的等待和重传由现有 KCP 可靠通道承担。
7. 玩家身份由服务端 connectionId 映射，客户端输入消息不提供可伪造的 playerId。非参战连接、未开战期间的输入，以及方向不在 -1/0/1 范围内的输入均忽略，不修改已保存方向。
8. 两端继续使用同一 KCP 可靠有序通道。相同方向重复到达只产生相同状态；不为本阶段添加输入序号、确认号或应用层重传。

## 客户端输入发送与网络等待

- GameEntry 每次 Unity 更新采集当前按键，得到 X/Z 各为 -1、0 或 1 的方向。
- 进入 Playing 后首次上传当前方向；之后仅在方向发生变化时上传，包括松键和失去焦点时的零方向。首次发送标记与上一份已提交方向在每局结束时清空，保证新局不会漏发相同方向。
- 只有 TrySend 成功提交时才更新已发送方向；提交失败且连接仍可用时，下一次更新重新尝试提交当前状态，不排队重放已过时的方向变化。
- 上传不依赖服务端新帧到达，也不创建客户端输入帧计数器、额外发送定时器或启动零输入流水线。
- 示例：服务器收到 W 后，帧 10、11、12 都可使用 W；松键消息在帧 13 封帧前到达，则帧 13 开始使用零方向。
- 第一阶段上传的是持续移动状态。同一服务端帧内到达的多次变化会合并到最后状态，极短按键可能不会产生位移；这不适合作为未来跳跃、射击等一次性指令的实现，需要在相应阶段单独设计。
- 服务器没有新输入消息时继续使用旧方向；客户端没有新的权威帧时保持当前位置。客户端不能自行沿用上一帧方向推进未知帧。
- 松键消息尚未到达服务器时仍可能继续移动，直到零方向被纳入权威帧。可靠重传由 KCP 负责，连接超时后按现有掉线规则中止整局；本阶段不另设输入超时归零规则。
- 不人为增加等待帧，但输入上传、服务端下次封帧和广播返回的耗时仍存在。本阶段接受这种响应延迟，后续通过本地预测改善。

## 协议

在现有 Config/lockstep.proto 中增加以下两种消息和一个帧输入结构，并用现有 Tools/gen-proto.bat 同时生成两端代码。

```proto
// MsgId 新增 C2S_Input = 8 和 S2C_Frame = 9。
message C2SInput {
  sint32 move_x = 1;
  sint32 move_z = 2;
}

message PlayerFrameInput {
  uint32 player_id = 1;
  sint32 move_x = 2;
  sint32 move_z = 3;
}

message S2CFrame {
  uint32 frame_id = 1;
  repeated PlayerFrameInput inputs = 2;
}
```

S2CMatchStart 新增 `repeated RoomPlayer players = 4`，由服务端下发按 PlayerId 升序的本局参战名单，客户端不再依赖大厅名单或自行排序；帧内输入与该名单同序。

开战保留 S2CMatchStart 的 TickHz 和 Seed，删除 input_delay_frames，以及 ServerConfig、MatchSettings、开战构造和客户端日志中对应引用。TickHz 和 Seed 保留各自现有字段号，不复用被删除字段号，不增加旧字段读取或兼容分支；两端使用新协议重新生成并一起更新。帧 1 为协议固定起点，不新增重复配置或确认帧字段。本阶段没有随机玩法，不为了使用 Seed 人为添加随机行为。其他已有字段和 reserved 声明不做无关清理。

## 组件职责与生命周期

### 服务端

- GameMain/FrameSync/FrameSyncComponent.cs：保存一条本局 S2CFrame，其中每名玩家一个 PlayerFrameInput（按 PlayerId 升序），并以 connectionId 索引到这些条目；收到输入直接改写，封帧时整条发送后帧号加一。
- GameMain/FrameSync/Handlers/InputHandler.cs：沿用现有泛型 Handler，将 C2SInput 交给帧同步组件。
- GameApplication 在现有 Root 上按依赖顺序创建组件，后创建的组件在 OnAwake 中取用前面的兄弟组件；配置在构造时校验，全部就绪后才监听网络。
- RoomComponent 负责入房与对局生命周期：PlayerId 按进房顺序递增，名单天然升序；人数达到门槛后广播携带名单的 MatchStart，再启动帧推进。
- 房间 Reset/AbortMatch 调用帧同步组件 Stop。AbortMatch 先重置房间再断开其余连接，主动断开会同步回调断线事件，此时名单已空。
- kcp2k 的 Send 不会同步触发断线（只有空消息才会，而 Packet 总带非零 MsgId），因此广播与封帧不再做重入保护；只有主动 Disconnect 会同步回调。
- 第一阶段服务器不维护角色坐标，不增加角色模拟副本；后续预测校正的权威依据需在下一阶段明确，不假定此处已具备完整状态校正能力。

### 客户端

- GameMain/Scripts/FrameSync/FrameSyncComponent.cs：按名单创建角色 Entity、方向变化时上传、校验并顺序执行权威帧、停止时清理；不引用 UnityEngine，只读暴露 Characters 与 LocalPlayerId。
- GameMain/Scripts/FrameSync/Handlers/FrameHandler.cs：沿用现有 Handler 形式转交 S2CFrame。
- GameMain/Scripts/Character/CharacterComponent.cs：唯一的角色组件，纯逻辑，保存 PlayerId 和整数 X/Z，由帧同步组件调用 Step 推进。
- GameApplication 负责组合，只对外暴露 FrameSync。RoomComponent 收到 MatchStart 后用其中的名单启动帧同步；Reset 时停止帧同步。
- GameEntry 是 Unity 入口：每次更新先采集 WASD（失焦为零）调用 SetInput，再更新 GameApplication，最后让同物体上的 GameRender 按逻辑角色刷新外观。
- GameRender 负责全部表现：检测到换局（角色数量或首个实例变化）时重建胶囊体，每帧把整数坐标写到 Transform，关闭时销毁。
- 服务端帧在网络回调中进入队列，随后在帧同步组件的 OnUpdate 中按顺序执行，每帧只执行一次。没有新帧就保持位置；收到多帧则按固定逻辑步长依次执行，而不是使用 Unity deltaTime 放大步长。
- 本阶段不单独添加客户端播放时钟或平滑层。网络积压后可能出现视觉跳动，但不跳过逻辑帧。
- 不新增服务定位器、通用命令系统、事件包装层或自定义定点数学库。

### 本阶段接口

必要接口按下面的形状落地，具体访问级别以实际调用最小化：

```csharp
// 服务端 FrameSyncComponent
void Start(IReadOnlyList<RoomMember> players, uint tickHz);
void OnInput(int connectionId, C2SInput message);
void Stop();

// 客户端 FrameSyncComponent
void Start(IReadOnlyList<RoomPlayer> players, uint localPlayerId, uint tickHz);
void SetInput(int moveX, int moveZ);
void ReceiveFrame(S2CFrame message);
void Stop();

// CharacterComponent，供帧同步组件逐帧调用
void Step(int moveX, int moveZ, uint tickHz);
```

以上方法均有真实业务边界和实际调用方，不额外增加仅为转发的一次性函数。

## 后续预测与回滚的阶段边界

预测回滚明确列入后续工作，本阶段的暂不实现不代表放弃该目标。

- 本阶段保持整数逻辑状态独立于 Transform，移动通过固定步长 Step 显式执行，服务端帧携带实际采用的全体输入。这些都是当前移动所需，也为后续重演提供清晰边界。
- 下一阶段再协商本地预测时钟、输入序号与权威确认之间的映射、状态快照、历史缓存、失配检测和回滚重演。届时需要修改协议和发送策略，不能直接把本阶段“仅方向变化时发送”当作完整预测输入历史。
- 后续权威基准如何取得需要单独设计：是客户端维护已确认帧的确定性状态，还是服务端同步模拟并下发状态；当前计划不提前决定或引入新架构。
- 历史状态保存、恢复接口、输入确认字段、回滚开关和通用命令系统本阶段均不预写；需要时以新计划添加，不写两套协议兼容逻辑。

## 整数移动与 Unity 表现

- 每 10000 个逻辑坐标单位表示 1 个 Unity 单位；位置使用 long，乘法中间值也使用 long。
- 建议第一版移动速度为 3 Unity 单位/秒，即 30000 逻辑单位/秒。
- 轴向方向系数为 10000，斜向两个分量均为 7071，以整数近似归一化，避免斜向快约 41%。相反方向同时按下时相互抵消。
- 每轴位移：30000L * direction / (10000L * tickHz)，使用 C# 整数除法。30 Hz 时轴向每帧 1000 单位、斜向每轴 707 单位；量化误差明确接受，不引入余数补偿系统。
- 两端按相同帧输入、相同整数初始位置和同一规则执行，确保本阶段移动在相同帧的整数结果一致；不声称未来浮点物理也具有此性质。
- 玩家按 PlayerId 排序后生成，沿 X 轴间隔 2 Unity 单位，围绕原点摆放，Y 固定；出生位置根据排序序号而非连续 playerId 假设计算。
- 在现有 SampleScene 中配置地面和俯视相机。GameRender 根据本局角色创建胶囊体，本地与远端使用不同颜色；只缓存实际需要的视图引用，停止本局时释放对象与自行创建的资源。
- 不新增 Prefab、材质资源或第二个场景，不使用 Rigidbody/CharacterController 驱动逻辑。整数位置是唯一移动状态，Transform 只负责显示。

## 文件清单

新增运行时代码共 6 个文件及 Unity 必要的 .meta：

1. Server/LockStep.Server/GameMain/FrameSync/FrameSyncComponent.cs
2. Server/LockStep.Server/GameMain/FrameSync/Handlers/InputHandler.cs
3. Client/LockStep/Assets/GameMain/Scripts/FrameSync/FrameSyncComponent.cs
4. Client/LockStep/Assets/GameMain/Scripts/FrameSync/Handlers/FrameHandler.cs
5. Client/LockStep/Assets/GameMain/Scripts/Character/CharacterComponent.cs
6. Client/LockStep/Assets/GameMain/Scripts/GameRender.cs（挂在 GameEntry 物体上）

修改范围：

- Config/lockstep.proto 及两端 Generated/Lockstep.cs。
- 两端 GameApplication.cs、RoomComponent.cs。
- Server/LockStep.Server/Config/ServerConfig.cs 删除 InputDelayFrames；两端房间代码移除 MatchSettings、开战消息和日志中的对应引用。
- Client/LockStep/Assets/GameMain/Scripts/GameEntry.cs。
- Client/LockStep/Assets/Scenes/SampleScene.unity，删除该场景中已无对应脚本字段的 roomId 序列化残留。
- 现有生成工具的中间输出按脚本处理，不新增其他生成工具。

不修改 Framework、不抽取共享程序集、不替换网络接口。已有 link.xml 的 GameMain.*Handler* 规则覆盖新增 FrameHandler，实施时核对即可。

## 实施顺序

- [x] 1. 扩展上述协议，删除 input_delay_frames 及其配置、构造和日志引用，运行现有生成脚本，核对两端生成结果一致。
- [x] 2. 实现服务端 FrameSyncComponent 和 InputHandler，接入房间开战与停止。
- [x] 3. 实现客户端 FrameSyncComponent、FrameHandler 和 CharacterComponent；接入 RoomComponent，完成首份方向发送、方向变化发送、连续帧校验、整数步进与角色清理。
- [x] 4. 在 GameEntry/GameRender 接通输入与表现，修改现有场景地面和相机。
- [x] 5. 整洁重构：逻辑层去除 Unity 依赖，合并角色组件，MatchStart 携带名单，删除冗余防御与未使用成员；服务端 dotnet build 与客户端脚本编译检查通过。
- [ ] 6. 用户 Unity 手动验收，确认后再决定下一阶段。

## 重点检查与手动验收

实施中的静态检查重点：重复方向、单帧内多次方向变化、松键与焦点丢失、非法方向或非成员输入、帧号跳跃、网络发送中同步断线；确认最终帧与最新方向一致，已生成帧不可被后续输入修改，上一局状态不会残留。

由用户在 Unity/客户端构建中执行：

1. 启动服务端和两个客户端，人齐后看到两个位置不同的角色；窗口在后台仍继续接收帧。
2. 分别操控两端，确认 WASD 只控制自己的角色，两端均能看到同样的移动；横向、纵向、斜向和相反键同时按下均符合规则。
3. 持续按住一个方向时，即使没有新的方向变化消息，角色也应持续移动；松键或切换窗口后，零方向经网络到达并进入权威帧后停止。
4. 同一逻辑帧比较两端 CharacterComponent 的整数 X/Z 应一致，不能仅用同一墙钟时刻的屏幕位置判断是否失步。
5. 网络延迟或短暂停顿时，缺少权威帧的客户端保持当前位置；服务端持续推进并沿用最新方向。恢复后新方向从后续封帧生效，客户端按序执行收到的权威帧，不自行推进缺失帧。
6. 关闭一个客户端，另一端按原房间规则断线并清理角色；重新启动两端后帧号从 1 开始，旧输入、旧角色和旧累计时间不残留。

## 当前交付状态

运行时代码与协议已实现并完成整洁重构。已执行：服务端 dotnet build、客户端脚本编译检查（基于 Unity 生成的 csproj 在临时目录编译）。尚未进行 Unity 运行或联机验证，由用户按上方步骤验收。
