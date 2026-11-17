// 索取并释放菜单输入，沿用既有角色、库存与音频入口。
using System.Collections.Generic;
using Ossastra.Characters;
using Ossastra.SideView.Slice;
using Ossastra.UserInterface;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ossastra.UI.GameMenu
{
    /// <summary>Consumes the scene's existing Menu action before gameplay reads the frame.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuInputAdapter : MonoBehaviour
    {
        public GameMenuRoot menu;

        [SerializeField] private InputAction previousPage = Button("MenuPreviousPage", "<Keyboard>/q", "<Gamepad>/leftShoulder");
        [SerializeField] private InputAction nextPage = Button("MenuNextPage", "<Keyboard>/e", "<Gamepad>/rightShoulder");
        [SerializeField] private InputAction previousContext = Button("MenuPreviousContext", "<Keyboard>/1", "<Gamepad>/leftTrigger");
        [SerializeField] private InputAction nextContext = Button("MenuNextContext", "<Keyboard>/3", "<Gamepad>/rightTrigger");
        [SerializeField] private InputAction submit = Button("MenuSubmit", "<Keyboard>/enter", "<Keyboard>/numpadEnter", "<Gamepad>/buttonSouth");
        [SerializeField] private InputAction back = Button("MenuBack", "<Gamepad>/buttonEast");
        [SerializeField] private InputAction recenter = Button("MenuMapRecenter", "<Keyboard>/tab", "<Gamepad>/start");
        [SerializeField] private InputAction rests = Button("MenuMapRests", "<Keyboard>/leftShift", "<Keyboard>/rightShift", "<Gamepad>/buttonNorth");
        [SerializeField] private InputAction clearMarkers = Button("MenuMapClearMarkers", "<Keyboard>/delete", "<Gamepad>/buttonWest");
        [SerializeField] private InputAction zoomIn = Button("MenuMapZoomIn", "<Keyboard>/pageUp", "<Gamepad>/rightStickPress");
        [SerializeField] private InputAction zoomOut = Button("MenuMapZoomOut", "<Keyboard>/pageDown", "<Gamepad>/leftStickPress");
        [SerializeField] private InputAction direction = Directions();
        [SerializeField] private InputAction pointerClick = Button("MenuPointerClick", "<Mouse>/leftButton");

        private readonly List<CharacterInputHandler> capturedInputs = new List<CharacterInputHandler>();
        private readonly MenuNavigationRepeat navigation = new MenuNavigationRepeat();
        private GameMenuRoot boundMenu;
        private InventoryView capturedInventory;
        private CharacterControlCoordinator capturedParty;
        private EventSystem navigationSystem;
        private GameObject previousSelection;
        private bool captured, audioOpen, menuActionsEnabled, previousNavigation, previousCursorVisible;
        private CursorLockMode previousCursorLock;
        private bool focused = true, applicationPaused;
        private int closedFrame = -1;

        public void Poll(InputAction menuAction, CharacterControlCoordinator party, InventoryView inventory)
        {
            if (!isActiveAndEnabled) return;
            BindMenu();
            if (boundMenu == null || !boundMenu.isActiveAndEnabled)
            {
                EndSession();
                return;
            }
            if (closedFrame >= 0 && closedFrame != Time.frameCount)
                ReleaseGameplay();
            if (!focused || applicationPaused) return;
            if (audioOpen && (capturedInventory == null || !capturedInventory.AudioSettingsOpen))
                ExitAudio();
            bool primaryCancel = Pressed(menuAction);
            if (primaryCancel || (boundMenu.IsOpen && Pressed(back)))
            {
                UpdateDeviceHint(primaryCancel ? menuAction : back);
                HandleCancel(party, inventory);
                return;
            }
            if (!boundMenu.IsOpen || audioOpen || closedFrame >= 0) return;
            SetExclusiveNavigation(true);
            DispatchPageControls();
        }

        public void EndSession()
        {
            if (boundMenu != null && boundMenu.IsOpen) boundMenu.Close();
            ReleaseGameplay();
        }

        private void BindMenu()
        {
            if (boundMenu == menu) return;
            EndSession();
            UnbindMenu();
            boundMenu = menu;
            if (boundMenu == null) return;
            var flow = SideViewSliceSessionFlow.Instance;
            if (flow != null && flow.GameActive)
                boundMenu.BindSaveAndReturn(() => flow != null && flow.RequestReturnToTitle());
            boundMenu.AudioRequested += EnterAudio;
            boundMenu.Closed += OnMenuClosed;
        }

        private void UnbindMenu()
        {
            if (boundMenu != null)
            {
                boundMenu.AudioRequested -= EnterAudio;
                boundMenu.Closed -= OnMenuClosed;
            }
            boundMenu = null;
        }

        private void HandleCancel(CharacterControlCoordinator party, InventoryView inventory)
        {
            var route = MenuCommandPolicy.Resolve(inventory == null || inventory.AllowsGameMenu,
                audioOpen, boundMenu.IsOpen, inventory != null && inventory.LegacySystemMenuOpen,
                inventory != null && inventory.IsOpen);
            switch (route)
            {
                case MenuCancelRoute.Commerce: return;
                case MenuCancelRoute.MenuAudio: ExitAudio(); return;
                case MenuCancelRoute.Menu: boundMenu.Cancel(); return;
                case MenuCancelRoute.LegacyMenu: inventory.CloseSystemMenu(); return;
                case MenuCancelRoute.Inventory: inventory.CancelInventoryFromGameMenu(); return;
                case MenuCancelRoute.Open: Open(party, inventory); return;
            }
        }

        private void Open(CharacterControlCoordinator party, InventoryView inventory)
        {
            if (party == null || captured || party.Characters.Count == 0) return;
            CaptureGameplay(party, inventory);
            var opened = false;
            try
            {
                opened = boundMenu.Open();
                if (!opened) return;
                SetMenuActions(true);
                navigation.RequireNeutral();
                SetExclusiveNavigation(true);
            }
            finally
            {
                if (!opened) ReleaseGameplay();
            }
        }

        private void CaptureGameplay(CharacterControlCoordinator party, InventoryView inventory)
        {
            captured = true;
            capturedParty = party;
            capturedInventory = inventory;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            capturedParty.SetInventoryCapture(this, true, preserveAcceptedMotion: true);
            if (capturedInventory != null) capturedInventory.SetGameMenuCapture(this, true);
            foreach (var member in capturedParty.Characters)
            {
                if (member == null || member.StateInput == null) continue;
                member.StateInput.SetInventoryCapture(this, true, true);
                capturedInputs.Add(member.StateInput);
            }
        }

        private void ReleaseGameplay()
        {
            closedFrame = -1;
            ClearAudio();
            SetMenuActions(false);
            SetExclusiveNavigation(false);
            if (!captured) return;
            captured = false;
            foreach (var input in capturedInputs)
                if (input != null) input.SetInventoryCapture(this, false, true);
            capturedInputs.Clear();
            if (capturedParty != null)
                capturedParty.SetInventoryCapture(this, false, preserveAcceptedMotion: true);
            if (capturedInventory != null)
                capturedInventory.SetGameMenuCapture(this, false);
            capturedParty = null;
            capturedInventory = null;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy
                    ? previousSelection : null);
            previousSelection = null;
        }

        private void OnMenuClosed()
        {
            // Keep capture through this frame so a cancel and a skill/shortcut cannot both act.
            closedFrame = Time.frameCount;
        }

        private void SetExclusiveNavigation(bool exclusive)
        {
            var current = EventSystem.current;
            if (navigationSystem != null && (!exclusive || navigationSystem != current))
            {
                navigationSystem.sendNavigationEvents = previousNavigation;
                navigationSystem = null;
            }
            if (!exclusive || current == null || navigationSystem == current) return;
            navigationSystem = current;
            previousNavigation = current.sendNavigationEvents;
            current.sendNavigationEvents = false;
        }

        private void EnterAudio()
        {
            if (!captured || audioOpen || capturedInventory == null) return;
            SetExclusiveNavigation(false);
            if (!capturedInventory.OpenGameMenuAudioSettings())
            {
                SetExclusiveNavigation(true);
                return;
            }
            audioOpen = true;
            boundMenu.SetOverlayVisible(false);
        }

        private void ClearAudio()
        {
            if (!audioOpen) return;
            audioOpen = false;
            if (capturedInventory != null) capturedInventory.CloseGameMenuAudioSettings();
        }

        private void ExitAudio()
        {
            ClearAudio();
            if (boundMenu == null || !boundMenu.IsOpen) return;
            boundMenu.SetOverlayVisible(true);
            navigation.RequireNeutral();
            SetExclusiveNavigation(true);
        }

        private void DispatchPageControls()
        {
            var step = MenuCommandPolicy.SignedEdge(Pressed(previousPage), Pressed(nextPage));
            if (step != 0)
            {
                UpdateDeviceHint(step < 0 ? previousPage : nextPage);
                boundMenu.MovePage(step);
                return;
            }
            step = MenuCommandPolicy.SignedEdge(Pressed(previousContext), Pressed(nextContext));
            if (step != 0)
            {
                UpdateDeviceHint(step < 0 ? previousContext : nextContext);
                boundMenu.ChangeContext(step);
                return;
            }
            if (Pressed(submit))
            {
                UpdateDeviceHint(submit);
                boundMenu.Submit();
                return;
            }
            if (DispatchAuxiliary(recenter, "map.recenter") || DispatchAuxiliary(rests, "map.rests") ||
                DispatchAuxiliary(clearMarkers, "map.clear-marker") || DispatchAuxiliary(zoomIn, "map.zoom-in") ||
                DispatchAuxiliary(zoomOut, "map.zoom-out")) return;
            var vector = direction != null ? direction.ReadValue<Vector2>() : Vector2.zero;
            var move = navigation.Sample(vector.x, vector.y, Time.unscaledTimeAsDouble);
            if (move != (0, 0))
            {
                UpdateDeviceHint(direction);
                boundMenu.Navigate(new Vector2Int(move.x, move.y));
            }
            else if (Pressed(pointerClick))
                UpdateDeviceHint(pointerClick);
        }

        private bool DispatchAuxiliary(InputAction action, string command)
        {
            if (!Pressed(action)) return false;
            UpdateDeviceHint(action);
            boundMenu.Auxiliary(command);
            return true;
        }

        private void UpdateDeviceHint(InputAction action)
        {
            var device = action?.activeControl?.device;
            if (device is Gamepad) boundMenu.SetDevice(true);
            else if (device is Keyboard || device is Mouse) boundMenu.SetDevice(false);
        }

        private IEnumerable<InputAction> Actions()
        {
            yield return previousPage;
            yield return nextPage;
            yield return previousContext;
            yield return nextContext;
            yield return submit;
            yield return back;
            yield return recenter;
            yield return rests;
            yield return clearMarkers;
            yield return zoomIn;
            yield return zoomOut;
            yield return direction;
            yield return pointerClick;
        }

        private void SetMenuActions(bool value)
        {
            if (menuActionsEnabled == value) return;
            menuActionsEnabled = value;
            foreach (var action in Actions())
            {
                if (action == null) continue;
                if (value) action.Enable();
                else action.Disable();
            }
        }

        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) EndSession();
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused) EndSession();
        }

        private void OnEnable() => InputSystem.onDeviceChange += DeviceChanged;

        private void DeviceChanged(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad && (change == InputDeviceChange.Removed ||
                change == InputDeviceChange.Disconnected || change == InputDeviceChange.Disabled))
                navigation.RequireNeutral();
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= DeviceChanged;
            EndSession();
            UnbindMenu();
        }

        private void OnDestroy()
        {
            foreach (var action in Actions()) action?.Dispose();
        }

        private static bool Pressed(InputAction action) => action != null && action.enabled && action.WasPressedThisFrame();

        private static InputAction Button(string name, params string[] paths)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (var path in paths) action.AddBinding(path);
            return action;
        }

        private static InputAction Directions()
        {
            var action = new InputAction("MenuDirection", InputActionType.Value, expectedControlType: "Vector2");
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            action.AddBinding("<Gamepad>/dpad");
            action.AddBinding("<Gamepad>/leftStick");
            return action;
        }
    }
}
