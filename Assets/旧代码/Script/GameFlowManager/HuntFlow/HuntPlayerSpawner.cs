using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HuntPlayerSpawner : MonoBehaviour, IHuntSubsystem
{
    private HuntFlowRoot huntFlowRoot;

    public GameObject actorPrefab;
    public CharacterSO playerSO;
    public Transform spawnPoint;

    [Header("Camera")]
    [SerializeField] private ProCameraRuntimeBinder cameraBinder;

    public bool HasPreparedNodeParty { get; private set; }

    public bool TryPrepareNodeParty()
    {
        var context = FindObjectOfType<DRPGNodeBattleContext>();
        HasPreparedNodeParty = false;
        if (context == null) return false;
        if (!context.TryValidate(out string error))
            throw new System.InvalidOperationException("Node party is not ready: " + error);
        HasPreparedNodeParty = true;
        if (cameraBinder != null)
        {
            cameraBinder.BindTarget(context.ExplorationCameraTarget);
            context.CenterExplorationCamera();
        }
        return true;
    }


    public void Initialize(HuntFlowRoot root)
    {
        huntFlowRoot = root;
        if (cameraBinder == null)
        {
            cameraBinder = FindObjectOfType<ProCameraRuntimeBinder>();
        }
    }

    public CharacterRoot SpawnPlayer()
    {
        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;

        GameObject go = Instantiate(actorPrefab, pos, Quaternion.identity);
        if (go == null)
        {
            Debug.LogError("[HuntPlayerSpawner] Instantiate actorPrefab ʧ��");
            return null;
        }
        CharacterRoot player = go.GetComponent<CharacterRoot>();
        if (player == null)
        {
            Debug.LogError("[HuntPlayerSpawner] ���ɵ� actorPrefab ��û�� CharacterRoot ���");
            return null;
        }

        if (playerSO == null)
        {
            Debug.LogError("[HuntPlayerSpawner] playerSO Ϊ��");
            return null;
        }

        //player.Initialize();
        player.InitializeWithSO(playerSO);

        player.characterDataBus.battleCamp = BattleCamp.MateSide;
        player.characterDataBus.currentHp = player.characterStatsController.GetStat(CharacterStats.MaxHP);//ͨ����������ֵ����ǽ�ɫ���״̬��ȷ�����ǻᱻ����������ȷʶ��ʹ���
        player.characterDataBus.canMove = true;

        player.skeletonAnimation.Skeleton.ScaleX = player.characterDataBus.FinalVisualScaleX;

        player.name = playerSO.characterName;

        player.stateArbitrator.SubmitRequest(
            new CharacterStateRequest(CharacterStateType.Idle, true)
        );

        huntFlowRoot.huntFlowDataBus.player = player;
        if (cameraBinder != null)
        {
            cameraBinder.BindTarget(player.transform);
        }
        else
        {
            Debug.LogWarning("[HuntPlayerSpawner] û���ҵ� ProCameraRuntimeBinder���������������");
        }
        //Debug.Log($"[HuntPlayerSpawner] ������ң�{player.name}");

        return player;
    }
}
