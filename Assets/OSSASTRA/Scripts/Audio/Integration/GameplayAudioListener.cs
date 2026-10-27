/* 脚本：GameplayAudioListener
 * 功能：为唯一音频根管理运行期听点，跟随当前实控角色，以水平朝向聆听XZ世界。
 * 边界：只读控制会话；不移动相机/角色，不写AudioListener.volume或AudioSettings，不保存场景。
 * 协作：根在LateUpdate先推进听点再推进声音；临时接替场景相机Listener，失效时精确恢复原enabled。
 */
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ossastra.Audio
{
    public sealed class GameplayAudioListener : IDisposable
    {
        private readonly Dictionary<AudioListener, bool> originalListeners = new Dictionary<AudioListener, bool>();
        private readonly GameObject anchor;
        private CharacterControlCoordinator coordinator;
        private Camera camera;
        private int sceneHandle = int.MinValue;
        private bool hasGameplayPosition;
        private bool disposed;
        public AudioListener Listener { get; }

        public GameplayAudioListener(GameObject owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            // 先临时让出原听点，避免创建时出现双Listener；只改运行实例，不改Prefab/相机Transform。
            SuspendSceneListeners();
            anchor = new GameObject("Gameplay Audio Listener [Audio Owned]");
            anchor.transform.SetParent(owner.transform, false);
            Listener = anchor.AddComponent<AudioListener>();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Tick();
        }

        public void Tick()
        {
            if (disposed) return;
            var scene = SceneManager.GetActiveScene();
            if (sceneHandle != scene.handle) hasGameplayPosition = false;
            if (sceneHandle != scene.handle || coordinator == null || !coordinator.isActiveAndEnabled)
            {
                sceneHandle = scene.handle;
                coordinator = null;
                foreach (var candidate in UnityEngine.Object.FindObjectsOfType<CharacterControlCoordinator>())
                    if (candidate.isActiveAndEnabled && candidate.gameObject.scene == scene) { coordinator = candidate; break; }
                camera = null;
                foreach (var candidate in UnityEngine.Object.FindObjectsOfType<Camera>())
                    if (candidate.isActiveAndEnabled && candidate.gameObject.scene == scene) { camera = candidate; break; }
            }
            var actor = coordinator == null ? null : coordinator.Current;
            if (actor != null && actor.isActiveAndEnabled)
            {
                anchor.transform.position = actor.transform.position + Vector3.up * .8f;
                hasGameplayPosition = true;
            }
            // 全员死亡/控制暂撤时保留本场最后听点，死亡尾音不能突然跳回高位相机。
            else if (!hasGameplayPosition && camera != null) anchor.transform.position = camera.transform.position;
            // 听点不随角色左右翻转；以镜头的地面前向保持画面左右与立体声一致。
            var forward = camera == null ? Vector3.forward : Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            anchor.transform.rotation = Quaternion.LookRotation(forward.sqrMagnitude < .001f ? Vector3.forward : forward, Vector3.up);
        }

        private void SuspendSceneListeners()
        {
            foreach (var candidate in UnityEngine.Object.FindObjectsOfType<AudioListener>(true))
            {
                if (candidate == Listener) continue;
                if (!originalListeners.ContainsKey(candidate)) originalListeners.Add(candidate, candidate.enabled);
                candidate.enabled = false;
            }
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { SuspendSceneListeners(); if (scene == SceneManager.GetActiveScene()) coordinator = null; Tick(); }
        private void OnActiveSceneChanged(Scene previous, Scene next) { SuspendSceneListeners(); sceneHandle = int.MinValue; Tick(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (Listener != null) Listener.enabled = false;
            foreach (var original in originalListeners) if (original.Key != null) original.Key.enabled = original.Value;
            originalListeners.Clear();
            if (anchor != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(anchor);
                else UnityEngine.Object.DestroyImmediate(anchor);
            }
        }
    }
}
