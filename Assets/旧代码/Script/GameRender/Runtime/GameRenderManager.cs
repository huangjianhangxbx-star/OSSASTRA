using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class GameRenderManager : MonoBehaviour
{
    [Header("唯一全局预设")]
    [SerializeField] private GameRenderProfile profile;
    [SerializeField] private Volume sceneVolume;
    [Header("预览")]
    [SerializeField] private bool enableNewStyle = true;
    [SerializeField] private bool previewInSceneView;
    [System.NonSerialized] private GameRenderProfile preview;
    [System.NonSerialized] private GameRenderProfile previewSource;
    [System.NonSerialized] private GameRenderVolumeBridge volumeBridge;

    public GameRenderProfile SourceProfile => profile;
    public Volume SceneVolume => sceneVolume;
    public VolumeProfile VolumePreview => volumeBridge != null ? volumeBridge.Preview : null;
    public GameRenderProfile Profile
    {
        get
        {
            EnsurePreview();
            return preview;
        }
    }
    public bool EnableNewStyle => enabled && gameObject.activeInHierarchy && enableNewStyle && profile != null;
    public bool PreviewInSceneView => previewInSceneView;

    private void OnEnable()
    {
        EnsurePreview();
        NotifyPreviewChanged();
    }

    private void OnValidate() => NotifyPreviewChanged();
    private void OnDisable()
    {
        GameRenderCharacterGlobals.Apply(null, false);
        RestoreVolumeBaseline();
        ReleasePreview();
        NotifyPreviewChanged();
    }

    private void OnDestroy()
    {
        GameRenderCharacterGlobals.Apply(null, false);
        RestoreVolumeBaseline();
        ReleasePreview();
    }

    public static GameRenderManager ForCamera(Camera camera)
    {
        if (camera == null) return null;
        var attached = camera.GetComponent<GameRenderManager>();
        if (attached != null) return attached;
        if (camera.cameraType != CameraType.SceneView) return null;
        foreach (var manager in FindObjectsOfType<GameRenderManager>(true))
            if (manager != null && manager.PreviewInSceneView && manager.EnableNewStyle)
                return manager;
        return null;
    }

    public void SetPreviewEnabled(bool value)
    {
        enableNewStyle = value;
        NotifyPreviewChanged();
    }

    public void NotifyPreviewChanged()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditor.SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
    }

    public void BeginVolumePreview()
    {
        if (sceneVolume == null) return;
        if (volumeBridge == null) volumeBridge = new GameRenderVolumeBridge();
        volumeBridge.Begin(sceneVolume);
    }

    public void RestoreVolumeBaseline()
    {
        volumeBridge?.Restore();
        volumeBridge = null;
    }

    public void ResetPreview()
    {
        ReleasePreview();
        EnsurePreview();
        NotifyPreviewChanged();
    }

    private void EnsurePreview()
    {
        if (preview != null && previewSource == profile) return;
        ReleasePreview();
        if (profile == null) return;
        preview = Instantiate(profile);
        preview.name = profile.name + " (unsaved GameRender preview)";
        // Hide the transient clone without making its Inspector properties read-only.
        preview.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
        previewSource = profile;
    }

    private void ReleasePreview()
    {
        if (preview != null)
        {
            if (Application.isPlaying) Destroy(preview);
            else DestroyImmediate(preview);
        }
        preview = null;
        previewSource = null;
    }
}
