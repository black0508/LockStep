using UnityEngine;
using Component = Framework.Component;

namespace GameMain
{
    // 挂在根实体上，每个渲染帧采集 WASD 方向；窗口失焦时视为松开。每个分量为 -1、0 或 1。
    public sealed class InputComponent : Component
    {
        public int MoveX { get; private set; }
        public int MoveZ { get; private set; }

        protected override void OnUpdate(float deltaTime)
        {
            if (!Application.isFocused)
            {
                MoveX = 0;
                MoveZ = 0;
                return;
            }
            MoveX = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            MoveZ = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
        }
    }
}
