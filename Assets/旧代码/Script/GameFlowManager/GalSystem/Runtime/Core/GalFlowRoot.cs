using UnityEngine;

public sealed class GalFlowRoot : MonoBehaviour, IGameSubsystem
{
    [SerializeField] private GalDialogueView dialogueView;
    [SerializeField, Min(1)] private int maxQueuedConcurrentRequests = 3;

    private GalFlowStateMachine stateMachine;
    private GalConcurrentDispatcher dispatcher;
    private bool isInitialized;

    public bool IsInitialized => isInitialized;
    public bool IsActive => stateMachine != null && stateMachine.IsActive;
    public GalFlowState State => stateMachine != null
        ? stateMachine.State
        : GalFlowState.Inactive;
    public int QueuedConcurrentCount => dispatcher?.QueuedCount ?? 0;
    public GalPresentationMode? ActiveMode =>
        stateMachine?.ActiveRequest?.PresentationMode;

    private void Reset()
    {
        AutoGetComponents();
    }

    private void OnDestroy()
    {
        DisposeDispatcher();
        stateMachine = null;
        isInitialized = false;
    }

    private void Update()
    {
        if (isInitialized)
            dispatcher?.Tick(Time.unscaledDeltaTime);
    }

    public void Initialize(GameFlowRoot root)
    {
        if (isInitialized)
            return;

        AutoGetComponents();
        if (root == null || dialogueView == null)
        {
            Debug.LogError("[GalFlowRoot] 初始化失败：缺少 GameFlowRoot 或 GalDialogueView。", this);
            return;
        }

        dialogueView.BindButton();
        DisposeDispatcher();
        stateMachine = new GalFlowStateMachine(
            dialogueView,
            new GameplaySuspensionCoordinator(root),
            root.EventHub.Gal);
        dispatcher = new GalConcurrentDispatcher(
            stateMachine,
            root.EventHub.Gal,
            maxQueuedConcurrentRequests);
        dispatcher.ProgressChanged += dialogueView.SetConcurrentProgress;
        isInitialized = true;
    }

    public GalStartResult RequestGal(GalPlayRequest request)
    {
        if (!isInitialized || dispatcher == null)
            return GalStartResult.RejectedByFlow;

        return dispatcher.Request(request);
    }

    public void StopGal(GalStopReason reason = GalStopReason.Requested)
    {
        dispatcher?.Stop(reason);
    }

    private void AutoGetComponents()
    {
        if (dialogueView == null)
            dialogueView = GetComponentInChildren<GalDialogueView>(true);
    }

    private void DisposeDispatcher()
    {
        if (dispatcher == null)
            return;

        if (dialogueView != null)
            dispatcher.ProgressChanged -= dialogueView.SetConcurrentProgress;

        dispatcher.Dispose();
        dispatcher = null;
    }
}
