using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GalDebugHotkeyTrigger : MonoBehaviour
{
    [SerializeField] private GameFlowArbitrator gameFlowArbitrator;
    [SerializeField] private GalSequenceSO testSequence;
    [SerializeField] private GalSequenceSO fullscreenTestSequence;
    [SerializeField] private GalSequenceSO concurrentTestSequenceA;
    [SerializeField] private GalSequenceSO concurrentTestSequenceB;
    [SerializeField] private GalPresentationMode presentationMode =
        GalPresentationMode.PausedOverlay;

    private void Reset()
    {
        AutoGetComponents();
    }

    private void Awake()
    {
        AutoGetComponents();
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Keyboard.current == null)
        {
            return;
        }

        bool pPressed = Keyboard.current.pKey.wasPressedThisFrame;
        bool oPressed = Keyboard.current.oKey.wasPressedThisFrame;
        if (!pPressed && !oPressed)
            return;

        bool shiftPressed = Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;
        GalSequenceSO sequence;
        GalPlayRequest request;
        string hotkey;

        if (pPressed)
        {
            sequence = shiftPressed ? fullscreenTestSequence : testSequence;
            GalPresentationMode mode = shiftPressed
                ? GalPresentationMode.FullscreenExclusive
                : presentationMode;
            string source = shiftPressed
                ? "DebugHotkey:Shift+P"
                : "DebugHotkey:P";
            hotkey = shiftPressed ? "Shift+P" : "P";
            request = new GalPlayRequest(sequence, mode, source);
        }
        else
        {
            sequence = shiftPressed
                ? concurrentTestSequenceB
                : concurrentTestSequenceA;
            string source = shiftPressed
                ? "DebugHotkey:Shift+O"
                : "DebugHotkey:O";
            string key = shiftPressed
                ? "debug.concurrent.b"
                : "debug.concurrent.a";
            hotkey = shiftPressed ? "Shift+O" : "O";
            request = new GalPlayRequest(
                sequence,
                GalPresentationMode.ConcurrentOverlay,
                source,
                key,
                1f);
        }

        if (gameFlowArbitrator == null)
        {
            Debug.LogWarning(
                $"[GalDebugHotkeyTrigger] {hotkey} request failed: " +
                "GameFlowArbitrator is missing.",
                this);
            return;
        }

        if (sequence == null)
        {
            Debug.LogWarning(
                $"[GalDebugHotkeyTrigger] {hotkey} request failed: " +
                "the matching test Sequence is missing.",
                this);
            return;
        }

        GalStartResult result = gameFlowArbitrator.RequestGal(request);

        if (result != GalStartResult.Started &&
            result != GalStartResult.Busy &&
            result != GalStartResult.Queued &&
            result != GalStartResult.DuplicateIgnored)
        {
            Debug.LogWarning(
                $"[GalDebugHotkeyTrigger] {hotkey} request result: {result}",
                this);
        }
#endif
    }

    private void AutoGetComponents()
    {
        if (gameFlowArbitrator == null)
            gameFlowArbitrator = GetComponentInParent<GameFlowArbitrator>();
    }
}
