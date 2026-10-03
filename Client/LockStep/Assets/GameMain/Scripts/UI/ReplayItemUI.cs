using System;
using System.IO;
using System.Text;
using GameMain.Replay;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.UI
{
    public sealed class ReplayItemUI : MonoBehaviour
    {
        [SerializeField] Text details;
        [SerializeField] Button playButton;
        [SerializeField] Button deleteButton;

        public void Bind(ReplayFile.Entry entry, Action<string> play, Action<ReplayFile.Entry> delete)
        {
            if (entry.Info == null)
                details.text = Path.GetFileName(entry.Path) + "\n无法读取（详见日志）";
            else
            {
                var names = new StringBuilder();
                foreach (var player in entry.Info.MatchStart.Players)
                {
                    if (names.Length > 0) names.Append(" / ");
                    names.Append(player.NickName).Append(" (P").Append(player.PlayerId).Append(')');
                }
                details.text = $"{entry.Info.RecordedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}    {FormatDuration(entry.Info.Duration)}"
                    + $"\n{names}\n录制玩家：P{entry.Info.LocalPlayerId}";
            }
            playButton.interactable = entry.Info != null;
            playButton.onClick.AddListener(() => play(entry.Path));
            deleteButton.onClick.AddListener(() => delete(entry));
        }

        public static string FormatDuration(double seconds)
        {
            TimeSpan duration = TimeSpan.FromSeconds(seconds);
            return $"{(long)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        }
    }
}
