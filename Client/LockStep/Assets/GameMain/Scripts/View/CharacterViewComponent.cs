using GameMain.Character;
using UnityEngine;
using Component = LockStep.Framework.Component;

namespace GameMain.View
{
    // 可以理解为渲染层，同时持有模型层和逻辑层
    // 角色外观：持有预制体实例，把同一实体上 CharacterComponent 的整数坐标写到 Transform，随实体销毁。
    public sealed class CharacterViewComponent : Component
    {
        CharacterComponent character;
        Transform view;

        public void Init(Transform value)
        {
            character = Entity.GetComponent<CharacterComponent>();
            view = value;
            SyncPosition();
        }

        protected override void OnUpdate(float deltaTime)
        {
            SyncPosition();
        }

        protected override void OnDestroy()
        {
            if (view != null) Object.Destroy(view.gameObject);
        }

        // 高度沿用预制体自身的 Y，逻辑只决定 XZ 平面位置。
        void SyncPosition()
        {
            float scale = CharacterComponent.CoordinateScale;
            view.position = new Vector3(character.X / scale, view.position.y, character.Z / scale);
        }
    }
}
