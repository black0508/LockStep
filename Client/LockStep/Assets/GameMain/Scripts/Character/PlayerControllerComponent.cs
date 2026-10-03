using LockStep.Framework;

namespace GameMain.Character
{
    // 只挂在联网对局的本地角色上：逻辑帧发送输入时由帧同步向它取当前方向。
    public sealed class PlayerControllerComponent : Component
    {
        InputComponent input;

        public int MoveX => input.MoveX;
        public int MoveZ => input.MoveZ;

        public void Init(InputComponent value)
        {
            input = value;
        }
    }
}
