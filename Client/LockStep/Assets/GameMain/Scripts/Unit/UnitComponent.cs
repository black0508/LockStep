using System.Collections.Generic;
using Lockstep.Proto;
using Framework;

namespace GameMain
{
    // 本局玩家，按 PlayerId 唯一映射。出生和销毁不归帧同步。
    public sealed class UnitComponent : Component
    {
        readonly Dictionary<uint, Unit> units = new Dictionary<uint, Unit>();

        // players 为参战名单，已按 PlayerId 升序。调用前须写好 GameData。
        public void Start(IReadOnlyList<RoomPlayer> players)
        {
            Stop();
            GameData data = GameEntry.Application.Data;
            for (int i = 0; i < players.Count; i++)
            {
                uint playerId = players[i].PlayerId;
                long x = (2L * i - (players.Count - 1)) * Unit.CoordinateScale;
                var unit = new Unit(Entity.World, playerId, playerId == data.LocalPlayerId, x);
                units.Add(playerId, unit);
                GameEntry.Application.Events.FireNow(this, UnitSpawnedEventArgs.Create(unit));
            }
        }

        public bool TryGet(uint playerId, out Unit unit)
        {
            return units.TryGetValue(playerId, out unit);
        }

        public IReadOnlyCollection<Unit> GetAll()
        {
            return units.Values;
        }

        // 没有该玩家时立即失败。销毁单位实体时，挂在上面的表现组件随之销毁。
        public void Remove(uint playerId)
        {
            Unit unit = units[playerId];
            units.Remove(playerId);
            unit.Dispose();
        }

        // 销毁单位实体时，挂在上面的表现组件随之销毁。
        public void Stop()
        {
            foreach (Unit unit in units.Values) unit.Dispose();
            units.Clear();
        }
    }
}
