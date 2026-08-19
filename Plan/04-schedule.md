# 任务拆解（规划）

仍然不写代码。下面是以后动手的顺序和过关标准。

## 第 1 周 — 可跑 demo

| 天 | 做 | 过关 |
|---|---|---|
| D1 | Transport：Fantasy 同款 KCP 内核 + 握手 + Protocol Ping/Pong | Unity `NetPump` 与控制台能收发；**还没有** Session/Sim；不引用 Fantasy.Scene |
| D2 | Room + Session：Join / JoinAck / MatchStart | 两客户端拿到 0/1，同时收到 MatchStart |
| D3 | 锁帧调度，空输入也能推进 | 双方 `frame` 一起涨；未齐帧时逻辑停 |
| D4 | Sim 移动 + 边界 + hash | 相同输入下坐标一致，Checksum 相同 |
| D5 | 攻击、扣血、减速 | 一边打中另一边，hash 仍一致 |
| D6 | 3D View：Cube 插值、闪色、HUD | 无 Animator |
| D7 | 人工延迟、断线提示、Desync 红屏 | **冻结功能**，能录双窗对打 |

D1 的意义：证明网络底层可以单独存在。如果 D1 就把移动写进 `Send`，分层失败。

## 第 2 周 — 回滚 + C++ 服务器

| 天 | 做 | 过关 |
|---|---|---|
| D8 | `GameState` 快照 + 单机故意改输入再回滚 | 不需要网络也能测 Restore |
| D9 | 远程 sticky-last-input 预测 | 本机操作变跟手；确认仍以 FrameInputs 为准 |
| D10 | 真双端误预测（变向） | 回滚后 hash 与对方最终一致；HUD 有次数 |
| D11–12 | C++：UDP + KCP + 同一 proto + Room | 行为对齐 C# 服务器 |
| D13 | 客户端只改地址/进程，切 C++ 回归 | 移动/攻击/回滚仍过 |
| D14 | 笔记 | Lockstep vs Rollback vs 状态同步；没做的列出 |

## 第 1 周验收清单

- 三个进程：C# 服务器 + 两个 Unity 2022.3 客户端
- 3D 场地，两个立方体，WASD 移动，按键攻击，命中减速
- 不依赖 PhysX / `deltaTime` 推进逻辑
- 故意改一端 Sim 能在该帧 Desync
- HUD：frame、input delay、checksum、rtt

## 两周内不做

- Unity 里 P/Invoke C++ KCP
- 服务端再跑一份战斗
- 通用 Buff 框架、技能树
- 角色动画、Root Motion、动画状态机回滚
- 3 人、重连追帧、录像
- Mirror / Netcode for GameObjects（KCP 库可以用）
