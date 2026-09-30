using GameMain.FrameSync;
using GameMain.Net;
using GameMain.Room;
using GameMain.Replay;
using UnityEngine;

namespace GameMain
{
    // 自行挂到场景上：左上角只读显示房间与权威帧状态。
    public sealed class TestRender : MonoBehaviour
    {
        RoomComponent room;
        FrameSyncComponent frameSync;
        NetworkComponent network;
        ReplayComponent replay;
        GUIStyle readoutStyle;

        void OnEnable()
        {
            room = GameEntry.Get<RoomComponent>();
            frameSync = GameEntry.Get<FrameSyncComponent>();
            network = GameEntry.Get<NetworkComponent>();
            replay = GameEntry.Get<ReplayComponent>();
        }

        void OnGUI()
        {
            if (frameSync.Mode == FrameSyncComponent.SimulationMode.None) return;
            if (readoutStyle == null)
            {
                readoutStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                readoutStyle.normal.textColor = Color.white;
            }

            uint localPlayerId = frameSync.Mode == FrameSyncComponent.SimulationMode.Replay
                ? replay.PlaybackInfo.LocalPlayerId : room.LocalPlayerId;
            string text = frameSync.Mode == FrameSyncComponent.SimulationMode.Replay
                ? $"Replay  player={localPlayerId}  applied={frameSync.AppliedFrame}  {frameSync.TickHz}Hz"
                : $"{room.Phase}  player={localPlayerId}  input={frameSync.InputFrame}"
                    + $"  applied={frameSync.AppliedFrame}  {frameSync.TickHz}Hz  RTT={network.RttMilliseconds}ms";
            for (int i = 0; i < frameSync.Characters.Count; i++)
            {
                var character = frameSync.Characters[i];
                string mark = character.PlayerId == localPlayerId ? "*" : "";
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
