using UnityEngine;
using Component = Framework.Component;

namespace GameMain
{
    // 单位外观：持有预制体实例，把 Unit 的整数坐标写到 Transform，随实体销毁。
    public sealed class UnitViewComponent : Component
    {
        Unit unit;
        Transform view;

        public void Init(Unit value, Transform viewValue)
        {
            unit = value;
            view = viewValue;
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
            float scale = Unit.CoordinateScale;
            view.position = new Vector3(unit.X / scale, view.position.y, unit.Z / scale);
        }
    }
}
