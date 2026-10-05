using System.Collections.Generic;
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
            GameData data = application.Data;
            NetworkComponent network = application.Network;
            UnitComponent units = application.Units;
            if (readoutStyle == null)
            {
                readoutStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                readoutStyle.normal.textColor = Color.white;
            }

            uint localPlayerId = data.LocalPlayerId;
            string text = data.Phase == GamePhase.Replay
                ? $"Replay  player={localPlayerId}  applied={frameSync.AppliedFrame}  {FrameSyncComponent.TickHz}Hz"
                : $"{data.Phase}  player={localPlayerId}  input={frameSync.InputFrame}"
                    + $"  applied={frameSync.AppliedFrame}  {FrameSyncComponent.TickHz}Hz  RTT={network.RttMilliseconds}ms";
            IReadOnlyCollection<Unit> all = units.GetAll();
            foreach (Unit unit in all)
            {
                string mark = unit.PlayerId == localPlayerId ? "*" : "";
                text += $"\nP{unit.PlayerId}{mark}  x={unit.X}  z={unit.Z}";
            }

            var area = new Rect(8, 8, 720, 24 * (all.Count + 1));
            Color previous = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(area.x + 1, area.y + 1, area.width, area.height), text, readoutStyle);
            GUI.color = Color.white;
            GUI.Label(area, text, readoutStyle);
            GUI.color = previous;
        }
    }
}
