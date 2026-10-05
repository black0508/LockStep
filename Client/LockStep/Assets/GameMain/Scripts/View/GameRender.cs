using Framework;
using UnityEngine;

namespace GameMain
{
    // 挂在 GameEntry 物体上的预制体提供者：单位生成时实例化外观，并把表现组件挂到该单位实体上。
    public sealed class GameRender : MonoBehaviour
    {
        [SerializeField] GameObject localCharacterPrefab;
        [SerializeField] GameObject remoteCharacterPrefab;

        void OnEnable()
        {
            GameEntry.Application.Events.Subscribe(UnitSpawnedEventArgs.EventId, OnUnitSpawned);
        }

        void OnDisable()
        {
            // 场景销毁时 GameEntry 及其事件组件可能已先释放。
            GameEntry.Application?.Events.Unsubscribe(UnitSpawnedEventArgs.EventId, OnUnitSpawned);
        }

        void OnUnitSpawned(object sender, GameEventArgs args)
        {
            var spawned = (UnitSpawnedEventArgs)args;
            GameObject view = Instantiate(spawned.Unit.IsLocal ? localCharacterPrefab : remoteCharacterPrefab, transform);
            view.name = $"Player {spawned.Unit.PlayerId}";
            spawned.Unit.Entity.AddComponent<UnitViewComponent>().Init(spawned.Unit, view.transform);
        }
    }
}
