using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

internal sealed class BattleCloseupRenderLayer : IDisposable
{
    internal const string SpectatorLayer = "BattleCloseupSpectator";
    internal const string CompositeLayer = "BattleCloseupComposite";
    internal const string ParticipantLayer = "BattleCloseupParticipant";

    private readonly struct RendererBaseline
    {
        internal RendererBaseline(SpriteRenderer renderer)
        { Renderer = renderer; Layer = renderer.sortingLayerID; Order = renderer.sortingOrder; }
        internal SpriteRenderer Renderer { get; }
        internal int Layer { get; }
        internal int Order { get; }
    }

    private readonly Camera camera;
    private readonly BattleCloseupProfile profile;
    private readonly Dictionary<int, BattleWorldActorView> views;
    private readonly Dictionary<int, bool> participantIds = new Dictionary<int, bool>();
    private readonly Dictionary<int, RendererBaseline> renderers = new Dictionary<int, RendererBaseline>();
    private readonly Dictionary<Light2D, int[]> lightLayers = new Dictionary<Light2D, int[]>();
    private GameObject overlay;
    private Material material;
    private Texture2D whiteTexture;
    private Sprite whiteSprite;
    private bool disposed;

    internal BattleCloseupRenderLayer(Camera camera, Dictionary<int, BattleWorldActorView> views,
        IReadOnlyList<ParticipantId> participants, BattleCloseupProfile profile)
    {
        this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
        this.views = views ?? throw new ArgumentNullException(nameof(views));
        this.profile = profile;
        for (int i = 0; i < participants.Count; i++) participantIds[participants[i].Value] = true;
        if (SortingLayer.NameToID(SpectatorLayer) == 0 || SortingLayer.NameToID(CompositeLayer) == 0 ||
            SortingLayer.NameToID(ParticipantLayer) == 0)
            throw new InvalidOperationException("Battle close-up sorting layers are not installed.");
        Shader shader = Shader.Find("ANKALUTE/BattleCloseupComposite");
        if (shader == null) throw new InvalidOperationException("Battle close-up composite shader is unavailable.");
        material = new Material(shader) { name = "BattleCloseupComposite(Runtime)" };
        material.SetFloat("_BlurStrength", profile.BlurStrength);
        material.SetFloat("_SpectatorDim", profile.SpectatorDim);
        material.SetFloat("_ReturnProgress", 0f);
        ExtendActorLighting();
        CreateOverlay();
        RefreshSorting();
    }

    internal void IncludeParticipant(int runtimeId)
    {
        participantIds[runtimeId] = true;
        RefreshView(runtimeId);
    }

    internal void Tick()
    {
        if (disposed) return;
        FitOverlay();
        RefreshSorting();
    }

    internal void ApplyReturn(float progress)
    {
        if (disposed || material == null) return;
        material.SetFloat("_ReturnProgress", Mathf.Clamp01(progress));
    }

    private void RefreshSorting()
    {
        foreach (int runtimeId in views.Keys) RefreshView(runtimeId);
    }

    private void RefreshView(int runtimeId)
    {
        if (!views.TryGetValue(runtimeId, out BattleWorldActorView view) || view == null) return;
        bool participant = participantIds.ContainsKey(runtimeId);
        foreach (SpriteRenderer renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null) continue;
            int instance = renderer.GetInstanceID();
            if (!renderers.ContainsKey(instance)) renderers.Add(instance, new RendererBaseline(renderer));
            renderer.sortingLayerName = participant ? ParticipantLayer : SpectatorLayer;
        }
    }

    private void CreateOverlay()
    {
        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "BattleCloseupWhite(Runtime)" };
        whiteTexture.SetPixel(0, 0, Color.white); whiteTexture.Apply(false, true);
        whiteSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        overlay = new GameObject("BattleCloseupComposite(Runtime)", typeof(SpriteRenderer));
        SpriteRenderer renderer = overlay.GetComponent<SpriteRenderer>();
        renderer.sprite = whiteSprite;
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = CompositeLayer;
        renderer.sortingOrder = 0;
        FitOverlay();
    }

    private void ExtendActorLighting()
    {
        FieldInfo field = typeof(Light2D).GetField("m_ApplyToSortingLayers", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) return;
        var sourceActorLayers = new HashSet<int>();
        foreach (BattleWorldActorView view in views.Values)
            if (view != null)
                foreach (SpriteRenderer renderer in view.GetComponentsInChildren<SpriteRenderer>(true))
                    sourceActorLayers.Add(renderer.sortingLayerID);
        int spectator = SortingLayer.NameToID(SpectatorLayer);
        int participant = SortingLayer.NameToID(ParticipantLayer);
        foreach (Light2D light in UnityEngine.Object.FindObjectsOfType<Light2D>(true))
        {
            int[] current = field.GetValue(light) as int[];
            if (current == null || !current.Any(sourceActorLayers.Contains)) continue;
            lightLayers[light] = (int[])current.Clone();
            field.SetValue(light, current.Concat(new[] { spectator, participant }).Distinct().ToArray());
        }
    }

    private void FitOverlay()
    {
        if (overlay == null || camera == null) return;
        float distance = Mathf.Max(camera.nearClipPlane + .1f, 1f);
        float height = camera.orthographic ? camera.orthographicSize * 2f :
            Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) * distance * 2f;
        float width = height * camera.aspect;
        overlay.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * distance, camera.transform.rotation);
        overlay.transform.localScale = new Vector3(width * 1.02f, height * 1.02f, 1f);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (RendererBaseline baseline in renderers.Values)
            if (baseline.Renderer != null)
            { baseline.Renderer.sortingLayerID = baseline.Layer; baseline.Renderer.sortingOrder = baseline.Order; }
        FieldInfo lightField = typeof(Light2D).GetField("m_ApplyToSortingLayers", BindingFlags.Instance | BindingFlags.NonPublic);
        if (lightField != null)
            foreach (var pair in lightLayers)
                if (pair.Key != null) lightField.SetValue(pair.Key, pair.Value);
        Destroy(overlay); Destroy(whiteSprite); Destroy(whiteTexture); Destroy(material);
    }

    private static void Destroy(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }
}
