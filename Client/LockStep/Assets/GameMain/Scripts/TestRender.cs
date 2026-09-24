using GameMain.FrameSync;
using GameMain.Net;
using GameMain.Room;
using UnityEngine;

namespace GameMain
{
    // 自行挂到场景上：左上角只读显示房间与权威帧状态。
    public sealed class TestRender : MonoBehaviour
    {
        RoomComponent room;
        FrameSyncComponent frameSync;
        NetworkComponent network;
        GUIStyle readoutStyle;

        void OnEnable()
        {
            room = GameEntry.Get<RoomComponent>();
            frameSync = GameEntry.Get<FrameSyncComponent>();
            network = GameEntry.Get<NetworkComponent>();
        }

        void OnGUI()
        {
            if (readoutStyle == null)
            {
                readoutStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                readoutStyle.normal.textColor = Color.white;
            }

            string text = $"{room.Phase}  player={room.LocalPlayerId}  input={frameSync.InputFrame}"
                + $"  applied={frameSync.AppliedFrame}  {frameSync.TickHz}Hz  RTT={network.RttMilliseconds}ms";
            for (int i = 0; i < frameSync.Characters.Count; i++)
            {
                var character = frameSync.Characters[i];
                string mark = character.PlayerId == room.LocalPlayerId ? "*" : "";
                text += $"\nP{character.PlayerId}{mark}  x={character.X}  z={character.Z}";
            }

            var area = new Rect(8, 8, 720, 24 * (frameSync.Characters.Count + 1));
            Color previous = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(area.x + 1, area.y + 1, area.width, area.height), text, readoutStyle);
            GUI.color = Color.white;
            GUI.Label(area, text, readoutStyle);
            GUI.color = previous;
        }
    }
}
