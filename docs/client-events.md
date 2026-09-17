# 客户端事件、引用池与日志

事件组件挂在应用 Root 上，通过 `GameApplication.Events` 访问。网络组件直接从同实体获取它，房间组件初始化时订阅、销毁时注销。事件同步执行，不使用队列或线程锁。

## 引用池

Root 先创建 `ReferencePoolComponent`，再创建 `EventComponent`；通过 `GameApplication.ReferencePool` 访问池组件。每个组件实例拥有独立缓存，随 Root 逆序销毁时自动清空，不再使用静态池。

精简 API 参考 [GF ReferencePool](https://github.com/EllanJiang/GameFramework/blob/master/GameFramework/Base/ReferencePool/ReferencePool.cs)：

- `Acquire<T>()`：按具体类型获取空闲对象，没有缓存时创建。
- `Release(IReference)`：调用对象的 `Clear()` 清除数据，再归还；重复归还同一个空闲对象会报错。
- `RemoveAll()`：释放当前池的空闲缓存，不影响已经借出的对象；组件在 OnDestroy 中自动调用。

引用池只支持主线程上的普通 C# 引用对象。池销毁后不再借出对象；已经借出的对象仍可归还，此时只清理、不再缓存。`Clear()` 应当只清空自身数据和持有的引用，不发布事件、不操作引用池、不抛异常。池按使用峰值保留缓存，不包含预热、统计或容量配置。

## 事件参数

每个事件独立定义 Args，使用类型哈希作为本次运行期间的本地 ID。有数据时提供 `Create` 来初始化池中对象，并在 `Clear` 中还原：

```csharp
public sealed class RoomJoinFailedEventArgs : GameEventArgs
{
    public static readonly int EventId = typeof(RoomJoinFailedEventArgs).GetHashCode();
    public override int Id => EventId;
    public string Reason { get; private set; }

    public static RoomJoinFailedEventArgs Create(ReferencePoolComponent pool, string reason)
    {
        var args = pool.Acquire<RoomJoinFailedEventArgs>();
        args.Reason = reason;
        return args;
    }

    public override void Clear() { Reason = null; }
}
```

上面是扩展示例。现有连接事件没有数据，通过各自的 Create 获取参数并发布：

```csharp
var pool = Entity.GetComponent<ReferencePoolComponent>();
var events = Entity.GetComponent<EventComponent>();
events.Subscribe(NetworkConnectedEventArgs.EventId, OnConnected);
events.FireNow(this, NetworkConnectedEventArgs.Create(pool));
events.Unsubscribe(NetworkConnectedEventArgs.EventId, OnConnected);
```

**FireNow 接管 Args，最外层同步分发结束后自动清理并归还，包括没有订阅者、组件关闭和回调异常的情况。** Create 传入事件组件同 Root 上的池；调用方和订阅者不要再手动归还，也不能跨回调缓存 Args；需要长期保留的数据自行复制。同步嵌套转发同一个 Args 时，会等最外层调用结束才归还。

ID 只用于当前运行期间的本地事件路由，不用于网络协议、存档、录像或帧同步校验；不额外维护类型映射或检查哈希冲突。

## 分发与生命周期

- 相同委托重复订阅会警告并忽略；注销不存在的订阅安全。
- 每轮使用独立快照：新增订阅不加入当前轮，注销立即生效；注销后重新订阅也不会恢复旧快照项。
- 监听器异常直接向调用方传播并中止本轮分发，finally 负责归还 Args 和快照。事件组件、Root 或 World 开始关闭时立即停止分发。
- 事件 Args 和分发快照通过引用池复用；订阅记录不池化，避免仍在快照中的旧订阅被复用。
- World 的更新快照列表在每轮结束后清空并复用，保持新增组件下一轮更新的语义。

本次验证完成后，已按要求删除临时测试项目和编译产物。验证覆盖引用池清理/复用、重复归还、嵌套分发、异常/销毁归还、房间进出与重连；另用 Unity 2022.3.62f3 自带编译器检查客户端程序集。没有执行 Unity 场景或 IL2CPP 构建。

## 静态日志

`GameLog` 是全局静态类，不依赖 World、Component 或 GameApplication 的实例，不再注入日志对象：

```csharp
GameLog.Info("连接成功", nameof(NetworkComponent));
GameLog.Warning("房间已满");
GameLog.Error("处理失败", nameof(RoomComponent), exception);
GameLog.MinimumLevel = LogLevel.Warning;
```

Framework 默认向控制台输出。Unity 入口在创建应用前设置 `GameLog.Writer = UnityGameLog.Write`，后者也只是静态函数，将格式化后的日志转发给 Debug.Log/LogWarning/LogError。日志级别和输出函数是全局配置，World 的创建或销毁不会改变它们。