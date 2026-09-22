using GameMain.FrameSync;
using UnityEngine;

namespace GameMain
{
    // WASD 移动输入；窗口失焦时视为松开。
    public sealed class KeyboardMoveInput : IMoveInput
    {
        public void Read(out int moveX, out int moveZ)
        {
            if (!Application.isFocused)
            {
                moveX = 0;
                moveZ = 0;
                return;
            }
            moveX = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            moveZ = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
        }
    }
}
