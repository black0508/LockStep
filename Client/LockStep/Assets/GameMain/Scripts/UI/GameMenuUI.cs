using System.Collections.Generic;
using System.IO;
using Lockstep.Proto;
using Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain
{
    // TODO： UI这块写的代码非常的垃圾，后续看有无时间可以重构掉系统性的UI架构
    // 布局和控件引用由预制体保存；只绑定 UI 操作，不采集输入或执行模拟。
    public sealed class GameMenuUI : MonoBehaviour
    {
        enum Page { Main, Online, List, Playback }

        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject onlinePanel;
        [SerializeField] GameObject listPanel;
        [SerializeField] GameObject playbackPanel;
        [SerializeField] GameObject deletePanel;
        [SerializeField] Text mainMessage;
        [SerializeField] Text onlineStatus;
        [SerializeField] Text listMessage;
        [SerializeField] Text playbackStatus;
        [SerializeField] Text deleteMessage;
        [SerializeField] Button enterGameButton;
        [SerializeField] Button replayListButton;
        [SerializeField] Button leaveGameButton;
        [SerializeField] Text leaveGameLabel;
        [SerializeField] Button backToMenuButton;
        [SerializeField] Button leaveReplayButton;
        [SerializeField] Button confirmDeleteButton;
        [SerializeField] Button cancelDeleteButton;
        [SerializeField] Transform listContent;
        [SerializeField] ScrollRect listScroll;
        [SerializeField] ReplayItemUI itemPrefab;

        Page page;
        ReplayFile.Entry pendingDelete;
        // 离房和录制结束可能嵌套到达，分别保存后拼接，与先后顺序无关。
        string leaveText = "";
        string recordText = "";

        void Awake()
        {
            mainMessage.text = "";
            ShowPage(Page.Main);
        }

        void OnEnable()
        {
            enterGameButton.onClick.AddListener(EnterGame);
            replayListButton.onClick.AddListener(OpenList);
            leaveGameButton.onClick.AddListener(LeaveGame);
            backToMenuButton.onClick.AddListener(BackToMenu);
            leaveReplayButton.onClick.AddListener(LeaveReplay);
            confirmDeleteButton.onClick.AddListener(ConfirmDelete);
            cancelDeleteButton.onClick.AddListener(CancelDelete);
            EventComponent events = GameEntry.Application.Events;
            events.Subscribe(RoomLeftEventArgs.EventId, OnRoomLeft);
            events.Subscribe(RecordingEndedEventArgs.EventId, OnRecordingEnded);
        }

        void OnDisable()
        {
            enterGameButton.onClick.RemoveListener(EnterGame);
            replayListButton.onClick.RemoveListener(OpenList);
            leaveGameButton.onClick.RemoveListener(LeaveGame);
            backToMenuButton.onClick.RemoveListener(BackToMenu);
            leaveReplayButton.onClick.RemoveListener(LeaveReplay);
            confirmDeleteButton.onClick.RemoveListener(ConfirmDelete);
            cancelDeleteButton.onClick.RemoveListener(CancelDelete);
            // 场景销毁时 GameEntry 及其事件组件可能已先释放。
            EventComponent events = GameEntry.Application?.Events;
            events?.Unsubscribe(RoomLeftEventArgs.EventId, OnRoomLeft);
            events?.Unsubscribe(RecordingEndedEventArgs.EventId, OnRecordingEnded);
        }

        void Update()
        {
            if (page == Page.Online)
            {
                GamePhase phase = GameEntry.Application.Data.Phase;
                if (phase == GamePhase.Playing)
                {
                    leaveGameLabel.text = "结束对局并返回";
                    onlineStatus.text = "对局进行中 · 正在录制回放";
                }
                else
                {
                    leaveGameLabel.text = "取消并返回";
                    onlineStatus.text = phase == GamePhase.Joined
                        ? "已进入房间，等待另一位玩家…" : "正在连接并加入房间…";
                }
            }
            else if (page == Page.Playback)
            {
                ReplayFile file = GameEntry.Application.Replay.Playback;
                FrameSyncComponent frameSync = GameEntry.Application.FrameSync;
                string progress = ReplayItemUI.FormatDuration((double)frameSync.AppliedFrame / FrameSyncComponent.TickHz)
                    + " / " + ReplayItemUI.FormatDuration(file.Info.Duration);
                playbackStatus.text = (frameSync.AppliedFrame == file.Frames.Count
                    ? "播放结束  " : "正在播放  ") + progress;
            }
        }

        void OnRoomLeft(object sender, GameEventArgs args)
        {
            var left = (RoomLeftEventArgs)args;
            leaveText = left.Reason switch
            {
                RoomLeaveReason.Cancelled => "已取消连接或等待",
                RoomLeaveReason.MatchQuit => "",
                RoomLeaveReason.JoinRejected => "加入房间失败：" + (left.RejectReason switch
                {
                    JoinRejectReason.JoinRejectFull => "房间已满",
                    JoinRejectReason.JoinRejectPlaying => "对局已开始",
                    _ => "未知原因",
                }),
                RoomLeaveReason.JoinSendFailed => "进房失败：连接已断开",
                RoomLeaveReason.ConnectionLost => "连接失败或已断开",
                RoomLeaveReason.MatchDisconnected => "连接已断开，对局结束",
                _ => "",
            };
            RefreshMainMessage();
            if (page == Page.Online) ShowPage(Page.Main);
        }

        void OnRecordingEnded(object sender, GameEventArgs args)
        {
            recordText = ((RecordingEndedEventArgs)args).Result switch
            {
                ReplayRecordResult.Saved => "回放已保存",
                ReplayRecordResult.Empty => "本局尚无可保存的回放帧",
                _ => "回放保存失败（详见日志）",
            };
            RefreshMainMessage();
        }

        void RefreshMainMessage()
        {
            mainMessage.text = leaveText.Length > 0 && recordText.Length > 0
                ? leaveText + "\n" + recordText : leaveText + recordText;
        }

        void EnterGame()
        {
            if (page != Page.Main) return;
            leaveText = "";
            recordText = "";
            if (!GameEntry.Application.StartOnline())
            {
                mainMessage.text = "无法开始连接，请检查服务器地址与端口";
                return;
            }
            onlineStatus.text = "正在连接并加入房间…";
            ShowPage(Page.Online);
        }

        // 离房事件负责返回主页并显示结果。
        void LeaveGame()
        {
            GameEntry.Application.ExitOnline();
        }

        void OpenList()
        {
            ShowPage(Page.List);
            RefreshList();
        }

        void RefreshList()
        {
            foreach (Transform child in listContent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            List<ReplayFile.Entry> entries = ReplayFile.List();
            if (entries == null)
            {
                listMessage.text = "无法读取回放目录（详见日志）";
                return;
            }
            foreach (ReplayFile.Entry entry in entries)
                Instantiate(itemPrefab, listContent).Bind(entry, Play, RequestDelete);
            listMessage.text = entries.Count == 0 ? "暂无回放。进入游戏后会自动录制。" : $"共 {entries.Count} 个回放";
            listScroll.verticalNormalizedPosition = 1;
        }

        void Play(string path)
        {
            if (page != Page.List || deletePanel.activeSelf) return;
            if (!GameEntry.Application.StartReplay(path))
            {
                listMessage.text = "无法播放此回放（详见日志）";
                return;
            }
            playbackStatus.text = "正在播放";
            ShowPage(Page.Playback);
        }

        void LeaveReplay()
        {
            GameEntry.Application.ExitReplay();
            OpenList();
        }

        void BackToMenu()
        {
            mainMessage.text = "";
            ShowPage(Page.Main);
        }

        void RequestDelete(ReplayFile.Entry entry)
        {
            pendingDelete = entry;
            string label = entry.Info == null ? Path.GetFileName(entry.Path)
                : entry.Info.RecordedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            deleteMessage.text = "确定删除此回放？\n" + label + "\n删除后无法恢复。";
            deletePanel.SetActive(true);
        }

        void ConfirmDelete()
        {
            if (pendingDelete == null) return;
            string path = pendingDelete.Path;
            CancelDelete();
            if (!ReplayFile.Delete(path))
            {
                listMessage.text = "删除回放失败（详见日志）";
                return;
            }
            RefreshList();
        }

        void CancelDelete()
        {
            pendingDelete = null;
            deletePanel.SetActive(false);
        }

        void ShowPage(Page value)
        {
            page = value;
            mainPanel.SetActive(value == Page.Main);
            onlinePanel.SetActive(value == Page.Online);
            listPanel.SetActive(value == Page.List);
            playbackPanel.SetActive(value == Page.Playback);
            CancelDelete();
        }
    }
}
