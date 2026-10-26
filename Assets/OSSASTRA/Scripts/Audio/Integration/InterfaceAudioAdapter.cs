/* 脚本：InterfaceAudioAdapter
 * 功能：只读背包与容器的真实打开状态，把状态边沿提交给唯一音频根。
 * 边界：不采按键、不修改UI/库存，不把Changed刷新解释为成功事务；没有自有播放器或协程。
 * 协作：仅A2/A3世界音频启用；首次发现、世界换代和租约重连只重采基线，不补播历史操作。
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Ossastra.UserInterface;
using UnityEngine;

namespace Ossastra.Audio
{
    /// <summary>界面状态的纯观察器；只记录已观察基线，不拥有界面状态或库存事务。</summary>
    public sealed class InterfaceAudioEdges
    {
        private bool initialized, previousOpen;
        private string previousExternal;

        public string[] Observe(bool open, string external)
        {
            external = string.IsNullOrEmpty(external) ? null : external;
            if (!initialized)
            {
                initialized = true;
                previousOpen = open;
                previousExternal = external;
                return Array.Empty<string>();
            }
            bool inventoryChanged = open != previousOpen;
            bool containerChanged = !string.Equals(external, previousExternal, StringComparison.Ordinal);
            if (!inventoryChanged && !containerChanged) return Array.Empty<string>();

            var events = new List<string>(4);
            // 同帧换容器必须先关闭旧来源；关包先退容器，开包先打开背包表面。
            if (containerChanged && previousExternal != null) events.Add("interaction.container.close");
            if (inventoryChanged) events.Add(open ? "ui.inventory.open" : "ui.inventory.close");
            if (containerChanged && external != null) events.Add("interaction.container.open");
            previousOpen = open;
            previousExternal = external;
            return events.ToArray();
        }

        /// <summary>连接生命周期变化后丢弃旧基线；下一次Observe只采样当前状态。</summary>
        public void Reset()
        {
            initialized = false;
            previousOpen = false;
            previousExternal = null;
        }
    }

    [DefaultExecutionOrder(275), DisallowMultipleComponent]
    public sealed class InterfaceAudioAdapter : MonoBehaviour
    {
        private readonly InterfaceAudioEdges edges = new InterfaceAudioEdges();
        private GameAudioRoot audioRoot;
        private SfxSourceHandle source;
        private InventoryView view;
        private string world, observedExternal;
        private float nextDiscovery;
        private long sequence;

        public int Submitted { get; private set; }
        public int Accepted { get; private set; }
        public string LastEvent { get; private set; } = "尚未观察界面边沿";
        public bool IsConnected => audioRoot != null && source != null && !source.IsDisposed;

        private void LateUpdate()
        {
            var root = GameAudioRoot.Instance;
            if (!Application.isPlaying || root == null || !root.SfxAvailable || !root.WorldAudioEnabled)
            {
                Disconnect();
                return;
            }
            if (audioRoot != root || world != root.WorldScopeId || source == null || source.IsDisposed ||
                view == null || !view.isActiveAndEnabled)
            {
                Disconnect();
                if (Time.unscaledTime < nextDiscovery) return;
                nextDiscovery = Time.unscaledTime + .5f;
                var candidate = FindObjectsOfType<InventoryView>().FirstOrDefault(value =>
                    value.isActiveAndEnabled && value.gameObject.scene == gameObject.scene);
                if (candidate == null) return;
                var lease = root.RegisterSfxSource("interface-audio@" + GetInstanceID());
                if (lease == null) return;
                audioRoot = root;
                source = lease;
                world = root.WorldScopeId;
                view = candidate;
                observedExternal = view.OpenedExternal;
                edges.Observe(view.IsOpen, observedExternal);
                return;
            }

            var previousExternal = observedExternal;
            observedExternal = view.OpenedExternal;
            foreach (var eventId in edges.Observe(view.IsOpen, observedExternal))
            {
                var current = view.Session == null ? null : view.Session.Coordinator?.Current;
                var position = current == null ? transform.position : current.transform.position;
                if (eventId.StartsWith("interaction.container.", StringComparison.Ordinal) && view.Session != null)
                {
                    var containerId = eventId == "interaction.container.close" ? previousExternal : observedExternal;
                    var containerPosition = view.Session.ContainerPosition(containerId);
                    if (containerPosition.HasValue) position = containerPosition.Value;
                }
                // 事实身份属于此租约；既不借战斗队列，也不改变音频根生命周期。
                var decision = source.Submit(eventId, "interface:" + (++sequence), position);
                Submitted++;
                if (decision.Accepted) Accepted++;
                LastEvent = eventId + " / " + decision.Reason;
            }
        }

        private void Disconnect()
        {
            source?.Dispose();
            source = null;
            audioRoot = null;
            world = null;
            view = null;
            observedExternal = null;
            edges.Reset();
        }

        private void OnDisable() { Disconnect(); }
        private void OnDestroy() { Disconnect(); }
    }
}
