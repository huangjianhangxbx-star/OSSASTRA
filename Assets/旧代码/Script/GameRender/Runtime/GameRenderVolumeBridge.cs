using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class GameRenderVolumeBridge : IDisposable
{
    private Volume volume;
    private VolumeProfile previousInstance;
    private VolumeProfile preview;

    public VolumeProfile Preview => preview;
    public bool IsActive => volume != null && preview != null;

    public void Begin(Volume target)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (target == volume && preview != null) return;
        Restore();
        volume = target;
        previousInstance = target.HasInstantiatedProfile() ? target.profile : null;
        var source = previousInstance != null ? previousInstance : target.sharedProfile;
        if (source == null)
        {
            volume = null;
            throw new InvalidOperationException("Volume has no source profile: " + target.name);
        }
        preview = ScriptableObject.CreateInstance<VolumeProfile>();
        preview.name = source.name + " (unsaved GameRender Volume preview)";
        preview.hideFlags = HideFlags.HideAndDontSave;
        foreach (var component in source.components)
        {
            if (component == null) continue;
            var componentCopy = UnityEngine.Object.Instantiate(component);
            componentCopy.hideFlags = HideFlags.HideAndDontSave;
            preview.components.Add(componentCopy);
        }
        target.profile = preview;
    }

    public void Restore()
    {
        if (volume != null && volume.HasInstantiatedProfile() && volume.profile == preview)
            volume.profile = previousInstance;
        if (preview != null)
        {
            foreach (var component in preview.components) Destroy(component);
            Destroy(preview);
        }
        preview = null;
        previousInstance = null;
        volume = null;
    }

    public void Dispose() => Restore();

    private static void Destroy(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }
}
