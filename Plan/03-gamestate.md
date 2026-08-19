# GameState 与确定性

Sim 只活在客户端。两端用同一份 C# 逻辑、同一串确认输入，得到同一份状态。服务器不持有 `GameState`，只对比客户端报上来的 hash。

## 时钟与单位

| 项 | 值 | 说明 |
|---|---|---|
| Tick | 20 Hz | 每逻辑帧 50ms，与渲染帧无关 |
| 长度 | 毫米 `int32` | 表现层 `/ 1000` 成 Unity 米 |
| 平面 | XZ | Y 不进 Sim |
| 场地 | `[-4000, 4000]` mm | 与 `MatchStart.arena_half_extent_mm` 一致 |
| 朝向 | 整数度 `0..359` | 禁止 `Mathf.Atan2` 当逻辑源 |

本 demo **不消费随机数**。`MatchStart.seed` 占位，hash 也不混入 seed，除非以后真用到。

## 状态字段

两名玩家，固定下标 = `playerId`。

```text
GameState
  uint32 frame
  PlayerState[2] players

PlayerState
  int32 x_mm
  int32 z_mm
  int32 yaw_deg              // 0=朝世界 +Z，只允许八向表里的值
  int32 hp                   // 初始 100
  int32 radius_mm            // 400
  int32 base_speed_mm        // 每 tick 700（约 14m/s，demo 好认；可再调）
  int32 slow_ticks_left      // >0 视为减速
  int32 attack_cd_ticks      // >0 不能再挥
  int32 attack_active_ticks  // >0 本帧/近期挥击，给 View 闪色用，也进 hash
```

开局（`MatchStart` 之后、`frame=0` 执行前）：

```text
player 0: x=-1500, z=0, yaw=90（朝 +X / 朝向对方）
player 1: x=+1500, z=0, yaw=270
hp=100, 其余计数=0
```

具体 yaw 只要「面对面、两端相同」即可，实现时写死常量。

## 一帧 Step 顺序（必须固定）

同一帧里顺序一变，hash 就会分叉。按下述执行：

1. 递减 `slow_ticks_left`、`attack_cd_ticks`、`attack_active_ticks`（到 0 为止）
2. 移动：按本帧该玩家 `dx/dz` 更新位置与 yaw，再夹紧场地
3. 攻击：`attack==true` 且 `attack_cd_ticks==0` 则开火
4. `frame += 1`（或在 Step 开头把传入的 N 写进 `GameState.frame`，两端同一处，规划选定：**Step 结束时 `frame = 刚执行完的 N`**）

开火规则：

- 置 `attack_active_ticks = 2`，`attack_cd_ticks = 10`
- 用当前 `yaw_deg` 查八向表，得到整数前方 `(fx, fz)`（见下）
- 判定圆圆心：`(x_mm, z_mm) + (fx, fz) * range_mm / 1000`，其中 `range_mm = 900`
- 判定圆半径：`350`
- 对方也是圆（`radius_mm`）。两圆心距离平方 `<= (350 + 对方radius)^2` 且对方 `hp>0` 则命中：`hp -= 10`（下限 0），`slow_ticks_left = 20`
- 不用世界轴对齐盒，也不用 `Quaternion`：斜向 45° 时轴对齐盒会对不齐朝向，两端若一个用 AABB、一个用旋转盒就会 desync

减速：`slow_ticks_left>0` 时，本帧位移速度 = `base_speed_mm / 2`（整数除法）。

本 demo 只有这一种「buff」：它是 `PlayerState` 上的两个整数，不是组件系统。

## 移动与朝向（避开浮点 Atan2）

`dx/dz` 是 -1/0/1。

合成速度（整数，避免对角更快）：

```text
if dx==0 and dz==0:
    不移动，yaw 保持
else if dx!=0 and dz!=0:
    step = speed * 707 / 1000      // ≈ 1/√2
    x += dx * step
    z += dz * step
else:
    x += dx * speed
    z += dz * speed
```

八向表（有位移才改 `yaw_deg`；攻击的前方也只查这张表）：

```text
(dx, dz) → yaw_deg, 前方(fx, fz) 以「长度 1000」计
( 0,  1) →   0, (   0, 1000)
( 1,  1) →  45, ( 707,  707)
( 1,  0) →  90, (1000,    0)
( 1, -1) → 135, ( 707, -707)
( 0, -1) → 180, (   0,-1000)
(-1, -1) → 225, (-707, -707)
(-1,  0) → 270, (-1000,   0)
(-1,  1) → 315, (-707,  707)
```

`yaw_deg = 0` 表示朝世界 `+Z`。不要在逻辑里调 `Atan2` / `Quaternion`。静止攻击时用**当前** yaw 反查这张表的 `(fx, fz)`。

夹紧：`x,z` 限制在 `[-half+radius, half-radius]`，两周内玩家之间不做推挤（简化；攻击仍用半径做命中）。

## 校验和

算法：FNV-1a 64 位。种子 `14695981039346656037`，质数 `1099511628211`。

按**固定顺序**把每个 `int32`/`uint32` 以小端 4 字节喂入（`hash` 自己是结果，不喂）：

```text
frame
for p in players:          # 0 然后 1
    x_mm, z_mm, yaw_deg, hp
    slow_ticks_left, attack_cd_ticks, attack_active_ticks
```

`radius_mm`、`base_speed_mm` 是常量，**不进 hash**（进了也可以，但两端必须同常量）。

客户端每执行完确认帧就发 `Checksum`。一边改逻辑、另一边不改，必须在该帧被服务器判 `Desync`。

## 给 View 的姿态

Sim 不返回 `Transform`。View 每渲染帧读：

```text
SimPose
  float x = x_mm / 1000
  float z = z_mm / 1000
  float yaw_deg
  int hp
  bool slowed          // slow_ticks_left > 0
  bool attacking       // attack_active_ticks > 0
```

插值只发生在 View：逻辑坐标是台阶，Cube 可以朝下一姿态平滑。回滚时允许 Cube 瞬移回正确位置，两周不追求回滚时动画融合。

## 第 2 周快照（本文件先定数据，不写实现）

`GameState` 必须是可深拷贝的纯数据（两个 `PlayerState` 值类型即可）。

- 环形缓冲 32 帧
- `Clone()` 在每次确认或预测 `Step` 之前存
- 误预测：`Restore(frame)` 后从分歧帧重演到当前预测头
- buff 字段已经在状态里，不必单独做 Buff 快照系统

## Sim 禁止事项

- `float` 当逻辑位置（View 转换除外）
- `Time.deltaTime` / `FixedUpdate` 驱动 `Step` 次数
- PhysX / `CharacterController` 位移
- `UnityEngine.Random`
- 根据「本机是不是 player 0」走不同分支（除 View/相机）
