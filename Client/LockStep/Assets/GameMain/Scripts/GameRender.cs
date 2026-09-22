using GameMain.FrameSync.Events;
using GameMain.View;
using LockStep.Framework;
using UnityEngine;

namespace GameMain
{
    // 挂在 GameEntry 物体上的预制体提供者：角色生成时实例化外观，并把表现组件挂到该角色实体上。
    public sealed class GameRender : MonoBehaviour
    {
        [SerializeField] GameObject localCharacterPrefab;
        [SerializeField] GameObject remoteCharacterPrefab;

        EventComponent events;

        void OnEnable()
        {
            events = GameEntry.Get<EventComponent>();
            events.Subscribe(CharacterSpawnedEventArgs.EventId, OnCharacterSpawned);
        }

        void OnDisable()
        {
            events.Unsubscribe(CharacterSpawnedEventArgs.EventId, OnCharacterSpawned);
        }

        void OnCharacterSpawned(object sender, GameEventArgs args)
        {
            var spawned = (CharacterSpawnedEventArgs)args;
            GameObject view = Instantiate(spawned.IsLocal ? localCharacterPrefab : remoteCharacterPrefab, transform);
            view.name = $"Player {spawned.Character.PlayerId}";
            spawned.Character.Entity.AddComponent<CharacterViewComponent>().Init(view.transform);
        }
    }
}
