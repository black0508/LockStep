using GameMain.Character;
using GameMain.FrameSync;
using GameMain.Net;
using GameMain.Room;
using GameMain.Replay;
using UnityEngine;

namespace GameMain
{
    public sealed class TestRender : MonoBehaviour
    {
        GUIStyle readoutStyle;

        void OnGUI()
        {
            GameApplication application = GameEntry.Application;
            if (application == null) return;
            FrameSyncComponent frameSync = application.FrameSync;
            RoomComponent room = application.Room;
            NetworkComponent network = application.Network;
            ReplayComponent replay = application.Replay;
            if (readoutStyle == null)
            {
                readoutStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                readoutStyle.normal.textColor = Color.white;
            }

            ReplayFile.Header info = replay.Playback?.Info;
            uint localPlayerId = info?.LocalPlayerId ?? room.LocalPlayerId;
            string text = info != null
                ? $"Replay  player={localPlayerId}  applied={frameSync.AppliedFrame}  {FrameSyncComponent.TickHz}Hz"
                : $"{room.Phase}  player={localPlayerId}  input={frameSync.InputFrame}"
                    + $"  applied={frameSync.AppliedFrame}  {FrameSyncComponent.TickHz}Hz  RTT={network.RttMilliseconds}ms";
            foreach (var pair in frameSync.Characters)
            {
                CharacterComponent character = pair.Value.GetComponent<CharacterComponent>();
                string mark = pair.Key == localPlayerId ? "*" : "";
                text += $"\nP{pair.Key}{mark}  x={character.X}  z={character.Z}";
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
