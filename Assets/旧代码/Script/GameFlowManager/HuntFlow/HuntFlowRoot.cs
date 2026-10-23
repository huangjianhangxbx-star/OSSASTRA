using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntFlowRoot : MonoBehaviour
{
    [SerializeField] private GameFlowRoot gameFlowRoot;
    [field: SerializeField] public HuntFlowDataBus huntFlowDataBus { get; private set; }
    [field: SerializeField] public HuntPlayerSpawner huntPlayerSpawner { get; private set; }




    private bool isInitialized;
    private void Reset()
    {
        AutoGetComponents();
    }

    public void Initialize(GameFlowRoot root)
    {
        if (isInitialized) return;
        isInitialized = true;

        gameFlowRoot = root;




        AutoGetComponents();

        huntFlowDataBus ??= new HuntFlowDataBus();

        IHuntSubsystem[] subsystems = GetComponentsInChildren<IHuntSubsystem>(true);
        foreach (var subsystem in subsystems)
        {
            subsystem.Initialize(this);
        }

        //Debug.Log($"[RunFlowRoot] 初始化完成，子系统数量：{subsystems.Length}");
    }

    private void AutoGetComponents()
    {
        if (gameFlowRoot == null)
            gameFlowRoot = GetComponentInParent<GameFlowRoot>();

        if (huntPlayerSpawner == null)
            huntPlayerSpawner = GetComponentInChildren<HuntPlayerSpawner>(true);

    }



}
