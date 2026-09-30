using System;
using System.Collections.Generic;
using System.IO;
using GameMain.FrameSync;
using GameMain.Replay;
using GameMain.Room;
using LockStep.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace GameMain.UI
{
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

        GameApplication application;
        RoomComponent room;
        ReplayComponent replay;
        FrameSyncComponent frameSync;
        Page page;
        ReplayFile.Entry pendingDelete;
        bool onlineHadMatch;

        void Awake()
        {
            application = GameEntry.Application;
            room = GameEntry.Get<RoomComponent>();
            replay = GameEntry.Get<ReplayComponent>();
            frameSync = GameEntry.Get<FrameSyncComponent>();
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
        }

        void Update()
        {
            if (page == Page.Online)
            {
                if (room.Phase == RoomComponent.Status.None)
                {
                    mainMessage.text = room.Message + (onlineHadMatch ? "\n" + replay.Message : "");
                    ShowPage(Page.Main);
                    return;
                }
                if (room.Phase == RoomComponent.Status.Playing)
                {
                    onlineHadMatch = true;
                    leaveGameLabel.text = "结束对局并返回";
                    onlineStatus.text = replay.IsRecording ? "对局进行中 · 正在录制回放" : replay.Message;
                }
                else
                {
                    leaveGameLabel.text = "取消并返回";
                    onlineStatus.text = room.Phase == RoomComponent.Status.Joined
                        ? "已进入房间，等待另一位玩家…" : "正在连接并加入房间…";
                }
            }
            else if (page == Page.Playback)
            {
                if (replay.Playback == ReplayComponent.PlaybackStatus.Failed)
                {
                    string error = replay.Message;
                    application.ExitReplay();
                    OpenList();
                    listMessage.text = error;
                    return;
                }
                ReplayFile.Header info = replay.PlaybackInfo;
                string progress = ReplayItemUI.FormatDuration((double)frameSync.AppliedFrame / info.MatchStart.TickHz)
                    + " / " + ReplayItemUI.FormatDuration(info.Duration);
                playbackStatus.text = (replay.Playback == ReplayComponent.PlaybackStatus.Finished
                    ? "播放结束  " : "正在播放  ") + progress;
            }
        }

        void EnterGame()
        {
            if (page != Page.Main) return;
            onlineHadMatch = false;
            if (!application.StartOnline())
            {
                mainMessage.text = "无法开始连接，请检查服务器地址与端口";
                return;
            }
            onlineStatus.text = "正在连接并加入房间…";
            ShowPage(Page.Online);
        }

        void LeaveGame()
        {
            bool wasPlaying = room.Phase == RoomComponent.Status.Playing;
            application.ExitOnline();
            mainMessage.text = wasPlaying ? replay.Message : "已取消连接或等待";
            ShowPage(Page.Main);
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
            List<ReplayFile.Entry> entries;
            try { entries = ReplayFile.List(replay.DirectoryPath); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                listMessage.text = "无法读取回放目录：" + error.Message;
                GameLog.Error("读取回放列表失败", error);
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
            if (!application.StartReplay(path))
            {
                listMessage.text = replay.Message;
                return;
            }
            playbackStatus.text = "正在播放";
            ShowPage(Page.Playback);
        }

        void LeaveReplay()
        {
            application.ExitReplay();
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
            try { ReplayFile.Delete(path); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                listMessage.text = "删除失败：" + error.Message;
                GameLog.Error("删除回放失败", error);
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
