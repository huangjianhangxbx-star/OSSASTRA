using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the lifetime of the scene-space battle world while remaining a small
/// bridge on the UGUI presentation prefab. The instantiated world is always a
/// top-level scene object; it is never parented below a Canvas/RectTransform.
/// </summary>
public sealed class BattleWorldRuntimeHost : MonoBehaviour
{
    [SerializeField] private GameObject worldPrefab;
    [SerializeField] private BattleWorldOutputMode outputMode =
        BattleWorldOutputMode.DirectCamera;
    [SerializeField] private RawImage renderTextureOutput;
    [SerializeField] private RenderTexture renderTexture;

    private GameObject worldInstance;
    private Sprite pendingBackground;
    private BattleSceneCameraLease sceneCameraLease;
    private Camera sceneCameraOverride;
    private Camera ownedCamera;
    private Transform ownedShotOrigin;
    private DRPGNodeBattleContext nodeContext;

    public BattleWorldOutputMode OutputMode => outputMode;
    public GameObject WorldInstance => worldInstance;
    public bool HasWorld => worldInstance != null;
    public BattleCameraPresenter CameraPresenter =>
        Resolve<BattleCameraPresenter>();
    public Camera BattleCamera => sceneCameraOverride != null
        ? sceneCameraOverride
        : CameraPresenter != null
            ? CameraPresenter.BattleCamera
            : Resolve<Camera>();
    public bool IsInPlaceBattle => sceneCameraLease != null;
    public BattleStagePresenter StagePresenter => Resolve<BattleStagePresenter>();
    public BattleWorldActorPool ActorPool => Resolve<BattleWorldActorPool>();
    public BattleWorldRosterPresenter RosterPresenter =>
        Resolve<BattleWorldRosterPresenter>();
    public BattlePresentationResourceCatalog ResourceCatalog =>
        Resolve<BattlePresentationResourceCatalog>();

    public void Configure(
        GameObject prefab,
        BattleWorldOutputMode mode,
        RawImage optionalOutput,
        RenderTexture optionalTexture)
    {
        worldPrefab = prefab;
        outputMode = mode;
        renderTextureOutput = optionalOutput;
        renderTexture = optionalTexture;
    }

    public void EnsureWorld()
    {
        if (worldInstance != null)
        {
            RemoveDuplicateRuntimeRoots(worldInstance);
            return;
        }
        if (worldPrefab == null)
        {
            throw new InvalidOperationException(
                "BattleWorldRuntimeHost requires a scene-space world prefab.");
        }

        if (TryRecoverRuntimeWorld())
            return;

        worldInstance = Instantiate(worldPrefab);
        worldInstance.name = RuntimeWorldName;
        worldInstance.transform.SetParent(null, true);
        Scene hostScene = gameObject.scene;
        if (hostScene.IsValid() && hostScene.isLoaded &&
            worldInstance.scene != hostScene)
        {
            SceneManager.MoveGameObjectToScene(worldInstance, hostScene);
        }
        MarkRuntimeOnly(worldInstance);
        worldInstance.SetActive(false);
        ApplyOutputMode(false);
    }

    public void SetWorldActive(bool active)
    {
        EnsureWorld();
        worldInstance.SetActive(active);
        ApplyOutputMode(active);
        if (active)
            ApplyPendingBackground();
    }

    public void BeginInPlaceBattle(DRPGNodeBattleContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (!context.TryValidate(out string error))
            throw new InvalidOperationException(error);
        EndInPlaceBattle();
        EnsureWorld();
        BattleCameraPresenter presenter = CameraPresenter;
        ownedCamera = presenter != null ? presenter.BattleCamera : Resolve<Camera>();
        ownedShotOrigin = presenter != null ? presenter.ShotOrigin : null;
        nodeContext = context;
        worldInstance.transform.position = context.BattleOrigin.position;
        worldInstance.SetActive(true);
        StagePresenter?.Activate(null);
        Resolve<BattleWorldBackgroundPresenter>()?.Bind(null, context.SceneCamera);
        if (ownedCamera != null)
            ownedCamera.enabled = false;
        sceneCameraLease = new BattleSceneCameraLease(
            context.SceneCamera,
            context.ExplorationCameraDrivers);
        sceneCameraOverride = context.SceneCamera;
        presenter?.Configure(context.SceneCamera, context.BattleOrigin, preserveSceneFraming: true);
        RosterPresenter?.SetInPlaceSorting(true);
        RosterPresenter?.ConfigureNodeOrigins(context.PartyRoot.QueueRoot, context.EnemyRoot);
        ActorPool?.ConfigureBorrowedParty(context.PartyRoot);
        context.PartyRoot.SetExplorationEnabled(false);
        ApplyOutputMode(true);
    }

    public void EndInPlaceBattle()
    {
        if (sceneCameraLease == null && nodeContext == null)
            return;
        BattleCameraPresenter presenter = CameraPresenter;
        presenter?.Configure(ownedCamera, ownedShotOrigin);
        RosterPresenter?.SetInPlaceSorting(false);
        ActorPool?.ConfigureBorrowedParty(null);
        sceneCameraOverride = null;
        sceneCameraLease?.Dispose();
        sceneCameraLease = null;
        if (nodeContext != null &&
            nodeContext.Mode == DRPGNodeBattleMode.ExplorationEncounter)
        {
            nodeContext.PartyRoot.SetExplorationEnabled(true);
        }
        nodeContext = null;
        ownedCamera = null;
        ownedShotOrigin = null;
    }

    public void SetBackground(Sprite sprite)
    {
        pendingBackground = sprite;
        ApplyPendingBackground();
    }

    public T Resolve<T>() where T : Component
    {
        return worldInstance != null
            ? worldInstance.GetComponentInChildren<T>(true)
            : null;
    }

    public T[] ResolveAll<T>() where T : Component
    {
        return worldInstance != null
            ? worldInstance.GetComponentsInChildren<T>(true)
            : Array.Empty<T>();
    }

    private void ApplyOutputMode(bool active)
    {
        Camera camera = sceneCameraLease == null
            ? BattleCamera
            : ownedCamera;
        bool useRenderTexture =
            outputMode == BattleWorldOutputMode.RenderTexture;
        if (camera != null)
        {
            if (sceneCameraLease != null)
            {
                camera.targetTexture = null;
                camera.enabled = false;
            }
            else
            {
                camera.targetTexture = useRenderTexture ? renderTexture : null;
                camera.enabled = active;
            }
        }

        if (renderTextureOutput != null)
        {
            renderTextureOutput.texture = renderTexture;
            renderTextureOutput.gameObject.SetActive(active && useRenderTexture);
        }
    }

    private void ApplyPendingBackground()
    {
        if (worldInstance == null)
            return;
        BattleWorldBackgroundPresenter presenter =
            Resolve<BattleWorldBackgroundPresenter>();
        presenter?.Bind(pendingBackground, BattleCamera);
    }

    private void DestroyWorld()
    {
        if (worldInstance == null)
            return;
        if (Application.isPlaying)
            Destroy(worldInstance);
        else
            DestroyImmediate(worldInstance);
        worldInstance = null;
    }

    private string RuntimeWorldName => worldPrefab != null
        ? worldPrefab.name + " (Runtime)"
        : string.Empty;

    private bool TryRecoverRuntimeWorld()
    {
        string runtimeName = RuntimeWorldName;
        if (string.IsNullOrEmpty(runtimeName))
            return false;

        List<GameObject> matches = FindRuntimeRoots(runtimeName);
        if (matches.Count == 0)
            return false;

        worldInstance = matches[0];
        MarkRuntimeOnly(worldInstance);
        RemoveDuplicateRuntimeRoots(worldInstance, matches);
        worldInstance.SetActive(false);
        ApplyOutputMode(false);
        return true;
    }

    private void RemoveDuplicateRuntimeRoots(GameObject survivor)
    {
        string runtimeName = RuntimeWorldName;
        if (string.IsNullOrEmpty(runtimeName))
            return;
        RemoveDuplicateRuntimeRoots(
            survivor,
            FindRuntimeRoots(runtimeName));
    }

    private static void RemoveDuplicateRuntimeRoots(
        GameObject survivor,
        IReadOnlyList<GameObject> matches)
    {
        for (int i = 0; i < matches.Count; i++)
        {
            GameObject candidate = matches[i];
            if (candidate == null || candidate == survivor)
                continue;
            candidate.SetActive(false);
            if (Application.isPlaying)
                Destroy(candidate);
            else
                DestroyImmediate(candidate);
        }
    }

    private List<GameObject> FindRuntimeRoots(string runtimeName)
    {
        var result = new List<GameObject>();
        GameObject[] roots = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.transform.parent != null ||
                root.name != runtimeName)
            {
                continue;
            }
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(root))
                continue;
#endif
            result.Add(root);
        }

        Scene hostScene = gameObject.scene;
        result.Sort((left, right) =>
            RuntimeRootScore(right, hostScene).CompareTo(
                RuntimeRootScore(left, hostScene)));
        return result;
    }

    private static int RuntimeRootScore(GameObject candidate, Scene hostScene)
    {
        if (candidate == null)
            return int.MinValue;
        if (hostScene.IsValid() && candidate.scene == hostScene)
            return 2;
        return candidate.scene.IsValid() && candidate.scene.isLoaded ? 1 : 0;
    }

    private static void MarkRuntimeOnly(GameObject instance)
    {
        if (instance == null)
            return;
        instance.hideFlags |= HideFlags.DontSaveInEditor |
            HideFlags.DontSaveInBuild;
    }

    private void OnEnable()
    {
        if (worldInstance == null && worldPrefab != null)
            TryRecoverRuntimeWorld();
    }

    private void OnDisable()
    {
        if (worldInstance != null)
            SetWorldActive(false);
    }

    private void OnDestroy() => DestroyWorld();
}
