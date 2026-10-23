using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameFlowRoot : MonoBehaviour
{
    public static GameFlowRoot Instance { get; private set; }



    private bool isInitialized = false;

    [field: SerializeField] public GameFlowDataBus gameFlowDataBus { get; private set; }

    [field: SerializeField] public GameFlowArbitrator gameFlowArbitrator { get; private set; }
    [field: SerializeField] public HuntFlowRoot huntFlow { get; private set; }
    [field: SerializeField] public GalFlowRoot galFlow { get; private set; }
    [field: SerializeField] public DRPGProgressionRoot drpgProgression { get; private set; }
    [field: SerializeField] public DRPGEquipmentRuntimeRoot drpgEquipment { get; private set; }
    [field: SerializeField] public DRPGBattleFlowRoot drpgBattleFlow { get; private set; }
    [field: SerializeField] public DRPGGameLoopRuntimeRoot drpgGameLoop { get; private set; }

    [field: SerializeField] public GameAudioRoot audioRoot { get; private set; }
    [field: SerializeField] public AudioArbitrator audioArbitrator { get; private set; }



    public GameEventHub EventHub { get; private set; }


    private void Reset()
    {
        AutoGetComponent();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[GameFlowRoot] 场景中出现了多个 GameFlowRoot，销毁重复对象", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Initialize();
    }
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Initialize()
    {
        if (isInitialized) return;


        AutoGetComponent();
        EventHub = new GameEventHub();
        gameFlowDataBus = new GameFlowDataBus();

        if (audioRoot != null)
        {
            audioRoot.Initialize(this);
        }
        else
        {
            Debug.LogWarning("[GameFlowRoot] 没有找到 GameAudioRoot", this);
        }
        if (audioArbitrator != null)
        {
            audioArbitrator.Initialize(this);
        }
        else
        {
            Debug.LogWarning("[GameFlowRoot] 没有找到 AudioArbitrator", this);
        }

        // 1. 先初始化 BattleFlow
        if (huntFlow != null)
        {
            //battleFlow.Initialize(this);

            huntFlow.Initialize(this);
        }
        else
        {
            Debug.LogWarning("[GameFlowRoot] 没有找到 HuntFlowRoot", this);
        }

        if (galFlow != null)
        {
            galFlow.Initialize(this);
        }
        else
        {
            Debug.LogWarning("[GameFlowRoot] 未找到 GalFlowRoot，Gal 对话入口不可用。", this);
        }

        if (drpgProgression != null)
        {
            drpgProgression.Initialize();
        }

        if (drpgEquipment != null && drpgProgression != null)
        {
            drpgEquipment.Initialize(drpgProgression.Service);
        }

        if (drpgBattleFlow != null)
        {
            drpgBattleFlow.Initialize(this);
            drpgGameLoop?.Initialize(this);
        }
        else
        {
            Debug.LogWarning("[GameFlowRoot] 未找到 DRPGBattleFlowRoot，回合制战斗入口不可用。", this);
        }



        // 2. 再初始化流程仲裁器/状态机
        if (gameFlowArbitrator != null)
        {
            gameFlowArbitrator.Initialize(this);
        }
        else
        {
            Debug.LogError("[GameFlowRoot] 没有找到 GameFlowArbitrator", this);
        }

        Debug.Log("[GameFlowRoot] 初始化完成");

        isInitialized = true;
    }

    private void AutoGetComponent()
    {
        gameFlowArbitrator = GetOrAddComponent<GameFlowArbitrator>();

        if (audioRoot == null)
        {
            audioRoot = GetComponentInChildren<GameAudioRoot>(true);
        }

        if (audioArbitrator == null)
        {
            audioArbitrator = GetComponentInChildren<AudioArbitrator>(true);
        }


        if (huntFlow == null)
        {
            huntFlow = GetComponentInChildren<HuntFlowRoot>(true);
        }

        if (galFlow == null)
        {
            galFlow = GetComponentInChildren<GalFlowRoot>(true);
        }

        if (drpgBattleFlow == null)
        {
            drpgBattleFlow = GetOrAddComponent<DRPGBattleFlowRoot>();
        }

        if (drpgProgression == null)
        {
            drpgProgression = GetOrAddComponent<DRPGProgressionRoot>();
        }

        if (drpgEquipment == null)
        {
            drpgEquipment = GetOrAddComponent<DRPGEquipmentRuntimeRoot>();
        }

        if (drpgGameLoop == null)
        {
            drpgGameLoop = GetOrAddComponent<DRPGGameLoopRuntimeRoot>();
        }

    }

    private T GetOrAddComponent<T>() where T : Component
    {
        T comp = GetComponent<T>();

        if (comp == null)
        {
            comp = gameObject.AddComponent<T>();
        }

        return comp;
    }


}
