// 复用现有库存取消与音量设置入口。
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ossastra.UserInterface
{
    public sealed partial class InventoryView
    {
        private object gameMenuOwner;
        private int suppressGameMenuThrough = -1;

        public bool AllowsGameMenu => commerceMenuGate.AllowsMenu(Time.frameCount);
        public bool LegacySystemMenuOpen => menuPanel != null && menuPanel.gameObject.activeSelf;
        public bool BlocksInventoryShortcuts => gameMenuOwner != null || Time.frameCount <= suppressGameMenuThrough;

        /// <summary>Bank the Menu gesture's final frame so another inventory Action cannot consume it.</summary>
        public void SetGameMenuCapture(object owner, bool captured)
        {
            if (owner == null) return;
            if (captured)
            {
                if (gameMenuOwner == null) gameMenuOwner = owner;
                return;
            }
            if (!ReferenceEquals(gameMenuOwner, owner)) return;
            gameMenuOwner = null;
            suppressGameMenuThrough = Time.frameCount;
        }

        public void CancelInventoryFromGameMenu()
        {
            if (!IsOpen || HasCommerceCapture) return;
            // The configured InventoryCancel action retains keyboard Escape ownership.
            if (cancelAction != null && cancelAction.enabled && cancelAction.WasPressedThisFrame()) return;
            suppressGameMenuThrough = Time.frameCount;
            if (drag != null) CancelDrag();
            else SetOpen(false);
        }

        /// <summary>Expose the existing audio controls through the existing Menu owner's capture.</summary>
        public bool OpenGameMenuAudioSettings()
        {
            if (!isActiveAndEnabled || windows == null || AudioSettingsOpen || LegacySystemMenuOpen ||
                IsOpen || !AllowsGameMenu || gameMenuOwner == null) return false;
            if (audioSettingsPanel == null) BuildAudioSettings();
            RefreshAudioSettings();
            windows.gameObject.SetActive(true);
            audioSettingsPanel.gameObject.SetActive(true);
            audioSettingsPanel.SetAsLastSibling();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(audioVolumeSliders[0].interactable
                    ? audioVolumeSliders[0].gameObject : audioReturnButton.gameObject);
            return true;
        }

        public void CloseGameMenuAudioSettings()
        {
            CloseAudioSettings(false);
            if (windows != null && !LegacySystemMenuOpen)
                windows.gameObject.SetActive(IsOpen);
        }
    }
}
