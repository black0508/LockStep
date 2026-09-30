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

## 引用池

两端的 `Framework/ReferencePool` 移植自 Game Framework，保留四文件结构、按类型分池、队列复用、预分配、移除和统计 API。来源版权与许可见 [Game Framework MIT License](ThirdPartyNotices/GameFramework-LICENSE.md)。

接入差异：命名空间使用 `LockStep.Framework`，参数校验失败通过 `GameLog.Error` 记录并立即退出，`Acquire(Type)` 参数非法返回 `null`。对外接口的参数校验保留；`EnableStrictCheck` 默认关闭，两端启动时不再主动开启。需要排查重复归还时可显式开启，检查会在清理对象和修改池、统计之前拒绝重复归还。对象自身构造函数或 `Clear()` 的异常仍向调用方传播。

引用池按进程存活，事件参数和分发快照仍由 `EventComponent` 归还。`RemoveAll` 只清理某一类型的空闲引用，`ClearAll` 还会清除池和统计，应在所有借出引用归还后调用。当前仍按主线程使用，统计信息不作为多线程原子快照。

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
