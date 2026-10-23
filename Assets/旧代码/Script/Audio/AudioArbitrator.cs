using System.Collections.Generic;
using UnityEngine;

public class AudioArbitrator : MonoBehaviour, IGameSubsystem
{
    private Dictionary<AudioStateType, IAudioStateMachine> stateMachinesDictionary;
    private HashSet<IAudioStateMachine> uniqueStateMachines;
    private Queue<AudioRequest> requestQueue;

    private IAudioStateMachine activeStateMachine;

    private HuntAudioStateMachine huntAudioSM;

    private GameFlowRoot gameFlowRoot;
    private GameEventHub eventHub;
    private GameAudioRoot audioRoot;

    private bool isInitialized;

    public void Initialize(GameFlowRoot root)
    {
        if (isInitialized)
            return;

        gameFlowRoot = root;
        eventHub = root.EventHub;
        audioRoot = root.audioRoot;

        stateMachinesDictionary = new Dictionary<AudioStateType, IAudioStateMachine>();
        uniqueStateMachines = new HashSet<IAudioStateMachine>();
        requestQueue = new Queue<AudioRequest>();

        InitializeStateMachines();

        SetInitialState();

        eventHub.Hunt.Subscribe<HuntFlowStateChangedEvent>(OnHuntFlowStateChanged);

        isInitialized = true;
    }
    private void SetInitialState()
    {
        activeStateMachine = huntAudioSM;
        activeStateMachine.Enter();

        Debug.Log($"[AudioArbitrator] ³õÊ¼ÒôÆµ×´Ì¬»ú: {activeStateMachine.AudioStateMachineName}");
    }
    private void Update()
    {
        if (!isInitialized)
            return;

        float deltaTime = Time.deltaTime;

        ProcessRequestQueue();

        IAudioStateMachine newActiveMachine = DetermineActiveStateMachine();

        if (newActiveMachine != null && newActiveMachine != activeStateMachine)
        {
            if (activeStateMachine == null ||
                newActiveMachine.Priority > activeStateMachine.Priority ||
                !activeStateMachine.IsActive)
            {
                PerformStateTransition(activeStateMachine, newActiveMachine);
            }
        }

        if (activeStateMachine != null && activeStateMachine.IsActive)
        {
            activeStateMachine.Update(deltaTime);
        }
    }

    private void OnDestroy()
    {
        if (eventHub != null)
        {
            eventHub.Hunt.Unsubscribe<HuntFlowStateChangedEvent>(OnHuntFlowStateChanged);
        }
    }






    private void InitializeStateMachines()
    {
        huntAudioSM = new HuntAudioStateMachine();

        RegisterStateMachine(AudioStateType.HuntPrepare, huntAudioSM);
        RegisterStateMachine(AudioStateType.HuntLoadMap, huntAudioSM);
        RegisterStateMachine(AudioStateType.HuntCountdown, huntAudioSM);
        RegisterStateMachine(AudioStateType.HuntCombat, huntAudioSM);
        RegisterStateMachine(AudioStateType.HuntLevelUp, huntAudioSM);
        RegisterStateMachine(AudioStateType.HuntResult, huntAudioSM);
    }

    private void RegisterStateMachine(AudioStateType type, IAudioStateMachine machine)
    {
        if (machine == null)
        {
            Debug.LogError($"[AudioArbitrator] ³¢ÊÔ×¢²á¿ÕÒôÆµ×´Ì¬»ú: {type}");
            return;
        }

        if (!uniqueStateMachines.Contains(machine))
        {
            machine.Initialize(gameFlowRoot, audioRoot, eventHub);
            uniqueStateMachines.Add(machine);
        }

        stateMachinesDictionary[type] = machine;
    }

    private void OnHuntFlowStateChanged(HuntFlowStateChangedEvent e)
    {
        AudioStateType audioStateType = ConvertHuntFlowToAudioState(e.to);

        if (audioStateType == AudioStateType.None)
            return;

        SubmitRequest(new AudioRequest(audioStateType, audioStateType, true));
    }

    private AudioStateType ConvertHuntFlowToAudioState(GameFlowStateType flowStateType)
    {
        switch (flowStateType)
        {
            case GameFlowStateType.HuntPrepare:
                return AudioStateType.HuntPrepare;

            case GameFlowStateType.HuntLoadMap:
                return AudioStateType.HuntLoadMap;

            case GameFlowStateType.HuntCountdown:
                return AudioStateType.HuntCountdown;

            case GameFlowStateType.HuntCombat:
                return AudioStateType.HuntCombat;

            case GameFlowStateType.HuntLevelUp:
                return AudioStateType.HuntLevelUp;

            case GameFlowStateType.HuntResult:
                return AudioStateType.HuntResult;

            default:
                return AudioStateType.None;
        }
    }

    public bool SubmitRequest(AudioRequest request)
    {
        if (request.IsUrgent)
            return ProcessRequestImmediately(request);

        requestQueue.Enqueue(request);
        return true;
    }

    private void ProcessRequestQueue()
    {
        while (requestQueue.Count > 0)
        {
            AudioRequest request = requestQueue.Dequeue();
            ProcessRequestImmediately(request);
        }
    }

    private bool ProcessRequestImmediately(AudioRequest request)
    {
        if (!stateMachinesDictionary.TryGetValue(request.audioStateType, out IAudioStateMachine targetMachine))
        {
            Debug.LogWarning($"[AudioArbitrator] Ã»ÓÐÕÒµ½´¦Àí {request.audioStateType} µÄÒôÆµ×´Ì¬»ú");
            return false;
        }

        return targetMachine.RequestState(request.Data);
    }

    private IAudioStateMachine DetermineActiveStateMachine()
    {
        IAudioStateMachine highestPriorityMachine = null;
        int maxPriority = int.MinValue;

        foreach (IAudioStateMachine machine in uniqueStateMachines)
        {
            if (machine.IsActive && machine.Priority > maxPriority)
            {
                maxPriority = machine.Priority;
                highestPriorityMachine = machine;
            }
        }

        return highestPriorityMachine;
    }

    private void PerformStateTransition(IAudioStateMachine from, IAudioStateMachine to)
    {
        from?.Exit();

        activeStateMachine = to;
        activeStateMachine.Enter();
    }
}