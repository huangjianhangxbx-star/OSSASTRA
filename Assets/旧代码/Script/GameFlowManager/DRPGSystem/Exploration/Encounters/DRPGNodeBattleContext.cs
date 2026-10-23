using System;
using System.Collections.Generic;
using UnityEngine;

public enum DRPGNodeBattleMode
{
    ExplorationEncounter = 0,
    DirectBattle = 1
}

/// <summary>
/// Explicit scene seam shared by authored encounter points and direct battles.
/// </summary>
[DisallowMultipleComponent]
public sealed class DRPGNodeBattleContext : MonoBehaviour
{
    [SerializeField] private string nodeId = "development-node";
    [SerializeField] private DRPGNodeBattleMode mode;
    [SerializeField] private DRPGNodePartyRoot partyRoot;
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Transform battleOrigin;
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private DRPGNodeBattleBounds battleBounds =
        new DRPGNodeBattleBounds();
    [SerializeField] private List<MonoBehaviour> explorationCameraDrivers =
        new List<MonoBehaviour>();
    [SerializeField, Min(0f)] private float requiredLeftExtent = 5f;
    [SerializeField, Min(0f)] private float requiredRightExtent = 7f;
    [SerializeField] private string protagonistStableId;
    [SerializeField, Range(.1f, .4f)] private float sideCenterViewportOffset = .25f;
    private Transform explorationCameraTarget;
    private float authoredCameraHeight;

    private void Awake()
    {
        if (sceneCamera != null) authoredCameraHeight = sceneCamera.transform.position.y;
    }

    public Transform ExplorationCameraTarget
    {
        get
        {
            if (explorationCameraTarget == null)
            {
                var anchor = new GameObject("NodeCameraFollowAnchor");
                anchor.transform.SetParent(partyRoot.QueueRoot, false);
                explorationCameraTarget = anchor.transform;
            }
            UpdateExplorationCameraTarget();
            return explorationCameraTarget;
        }
    }

    private void LateUpdate()
    {
        if (explorationCameraTarget != null && partyRoot != null && partyRoot.ExplorationEnabled)
            UpdateExplorationCameraTarget();
    }

    private void UpdateExplorationCameraTarget()
    {
        if (sceneCamera == null || partyRoot == null || explorationCameraTarget == null) return;
        float depth = Vector3.Dot(partyRoot.transform.position - sceneCamera.transform.position, sceneCamera.transform.forward);
        float halfHeight = sceneCamera.orthographic ? sceneCamera.orthographicSize :
            Mathf.Tan(sceneCamera.fieldOfView * .5f * Mathf.Deg2Rad) * depth;
        var proCamera = sceneCamera.GetComponent<Com.LuisPedroFonseca.ProCamera2D.ProCamera2D>();
        // Cancel the existing global horizontal offset so this viewport position
        // has one owner. Preserve the authored vertical camera framing.
        float existingX = proCamera != null ? proCamera.GetOffsetX() : 0f;
        float existingY = proCamera != null ? proCamera.GetOffsetY() : 0f;
        var position = explorationCameraTarget.position;
        position.x = partyRoot.transform.position.x + 2f * halfHeight * sceneCamera.aspect * sideCenterViewportOffset - existingX;
        if (!cameraHeightCaptured)
        {
            explorationCameraTarget.position = new Vector3(position.x, authoredCameraHeight - existingY, partyRoot.transform.position.z);
            cameraHeightCaptured = true;
        }
        else
        {
            position.y = explorationCameraTarget.position.y;
            explorationCameraTarget.position = position;
        }
    }

    private bool cameraHeightCaptured;

    public void CenterExplorationCamera()
    {
        var proCamera = sceneCamera != null ? sceneCamera.GetComponent<Com.LuisPedroFonseca.ProCamera2D.ProCamera2D>() : null;
        if (proCamera != null) proCamera.CenterOnTargets();
    }

    public string NodeId => nodeId;
    public DRPGNodeBattleMode Mode => mode;
    public DRPGNodePartyRoot PartyRoot => partyRoot;
    public Camera SceneCamera => sceneCamera;
    public Transform BattleOrigin => battleOrigin != null
        ? battleOrigin
        : partyRoot != null ? partyRoot.QueueRoot : transform;
    public Transform EnemyRoot => enemyRoot;
    public DRPGNodeBattleBounds BattleBounds => battleBounds;
    public IReadOnlyList<MonoBehaviour> ExplorationCameraDrivers =>
        explorationCameraDrivers;

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return Fail("Node battle context requires a node id.", out error);
        if (partyRoot == null || partyRoot.Formation == null ||
            partyRoot.Formation.Count == 0)
        {
            return Fail("Node battle context requires a populated party root.", out error);
        }
        if (sceneCamera == null)
            return Fail("Node battle context requires the active scene camera.", out error);
        if (enemyRoot == null)
            return Fail("Node battle context requires an enemy root.", out error);
        if (battleBounds == null ||
            !battleBounds.ContainsBattle(
                partyRoot.QueueRoot.position.x,
                requiredLeftExtent,
                requiredRightExtent))
        {
            return Fail(
                "The encounter composition is outside the authored battle-safe bounds.",
                out error);
        }
        error = string.Empty;
        return true;
    }

    public void CaptureCurrentSceneComposition()
    {
        if (sceneCamera == null || battleOrigin == null)
            return;
        Vector3 cameraPosition = sceneCamera.transform.position;
        Vector3 originPosition = battleOrigin.position;
        originPosition.x = cameraPosition.x;
        originPosition.y = partyRoot.QueueRoot.position.y;
        originPosition.z = partyRoot.QueueRoot.position.z;
        battleOrigin.position = originPosition;
        // The party root is node state. A mode transition must never pull it
        // to the camera; authored encounter placement is preserved verbatim.
        if (enemyRoot != null)
        {
            Vector3 enemyPosition = enemyRoot.position;
            enemyPosition.x = 2f * originPosition.x - partyRoot.QueueRoot.position.x;
            enemyPosition.y = partyRoot.QueueRoot.position.y;
            enemyPosition.z = partyRoot.QueueRoot.position.z;
            enemyRoot.position = enemyPosition;
        }
    }

    public DRPGPartyRosterSnapshot CaptureRoster(DRPGPartySetupSO setup)
    {
        if (setup == null)
            throw new ArgumentNullException(nameof(setup));
        DRPGPartyRosterSnapshot roster =
            DRPGPartyCompositionState.FromSetup(setup).CreateSnapshot();
        return roster.WithPhysicalOrder(partyRoot.Formation.Capture());
    }

    public void ConfigureForRuntime(
        string id,
        DRPGNodeBattleMode battleMode,
        DRPGNodePartyRoot party,
        Camera camera,
        Transform origin,
        Transform enemies,
        DRPGNodeBattleBounds bounds,
        float leftExtent,
        float rightExtent)
    {
        nodeId = id;
        mode = battleMode;
        partyRoot = party;
        sceneCamera = camera;
        if (camera != null) authoredCameraHeight = camera.transform.position.y;
        battleOrigin = origin;
        enemyRoot = enemies;
        battleBounds = bounds;
        requiredLeftExtent = leftExtent;
        requiredRightExtent = rightExtent;
    }

    public bool AttachExplorationProtagonist(CharacterRoot character)
    {
        if (character == null || partyRoot == null ||
            string.IsNullOrWhiteSpace(protagonistStableId) ||
            !partyRoot.TryGetMember(protagonistStableId, out DRPGScenePartyMember member))
        {
            return false;
        }
        // A controller discovered by Hunt is not proof of party identity.
        // Only an explicitly authored node member may grant this binding.
        if (member.Character != character ||
            !character.transform.IsChildOf(partyRoot.QueueRoot))
            return false;
        BattleWorldActorView view = character.GetComponent<BattleWorldActorView>();
        if (view == null)
            view = character.gameObject.AddComponent<BattleWorldActorView>();
        view.EnsureAnchors();
        member.ConfigureForRuntime(protagonistStableId, character, view);
        return true;
    }

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
