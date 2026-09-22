using GameMain.FrameSync;
using GameMain.Room;
using UnityEngine;

namespace GameMain
{
    // 自行挂到场景上：左上角只读显示房间与权威帧状态。
    public sealed class TestRender : MonoBehaviour
    {
        RoomComponent room;
        FrameSyncComponent frameSync;
        GUIStyle readoutStyle;

        void OnEnable()
        {
            room = GameEntry.Get<RoomComponent>();
            frameSync = GameEntry.Get<FrameSyncComponent>();
        }

        void OnGUI()
        {
            if (room == null || frameSync == null) return;
            if (readoutStyle == null)
            {
                readoutStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                readoutStyle.normal.textColor = Color.white;
            }

            string text = $"{room.Phase}  player={room.LocalPlayerId}  frame={frameSync.FrameId}  {frameSync.TickHz}Hz";
            var characters = frameSync.Characters;
            for (int i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                string mark = character.PlayerId == room.LocalPlayerId ? "*" : "";
                text += $"\nP{character.PlayerId}{mark}  in={character.MoveX},{character.MoveZ}  x={character.X}  z={character.Z}";
            }

            var area = new Rect(8, 8, 720, 24 * (characters.Count + 1));
            Color previous = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(area.x + 1, area.y + 1, area.width, area.height), text, readoutStyle);
            GUI.color = Color.white;
            GUI.Label(area, text, readoutStyle);
            GUI.color = previous;
        }
    }
}
