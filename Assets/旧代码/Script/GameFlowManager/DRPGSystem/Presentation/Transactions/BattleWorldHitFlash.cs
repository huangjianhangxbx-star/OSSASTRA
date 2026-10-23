using System;
using System.Collections.Generic;
using UnityEngine;

// One short material pulse. It never writes actor color, alpha, pose or motion,
// so releasing it cannot undo DefeatPending dimming or cause a second tint hit.
internal sealed class BattleWorldHitFlash : IDisposable
{
    private readonly BattleWorldHitFlashMaterialCache cache;
    private readonly int pulseToken;
    private readonly double started;
    private bool disposed;

    internal static void Prepare(BattleWorldActorView target)
    {
        if (target == null || target.VisualRoot == null)
            return;
        var prepared = target.GetComponent<BattleWorldHitFlashMaterialCache>();
        if (prepared == null)
            prepared = target.gameObject.AddComponent<BattleWorldHitFlashMaterialCache>();
        prepared.Prepare(target.VisualRoot);
    }

    internal BattleWorldHitFlash(BattleWorldActorView target, double started)
    {
        this.started = started;
        Prepare(target);
        cache = target != null
            ? target.GetComponent<BattleWorldHitFlashMaterialCache>()
            : null;
        pulseToken = cache != null ? cache.BeginPulse() : 0;
    }

    internal void Tick(double now)
    {
        if (disposed) return;
        float remaining = 1f - Mathf.Clamp01((float)((now - started) / .09d));
        if (remaining <= 0) { Dispose(); return; }
        cache?.SetPulse(pulseToken, remaining * remaining);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        cache?.EndPulse(pulseToken);
    }
}

// Lives with the pooled actor view, so all supported sprite materials are
// created while the actor is bound rather than on the unique impact frame.
// A pulse only borrows them; returning the actor or destroying its view safely
// restores the original material.
internal sealed class BattleWorldHitFlashMaterialCache : MonoBehaviour
{
    private sealed class Binding
    {
        internal SpriteRenderer Renderer;
        internal Material Original;
        internal Material Pulse;
    }

    private static Shader shader;
    private readonly List<Binding> bindings = new List<Binding>();
    private int token;
    private bool active;

    internal void Prepare(Transform visualRoot)
    {
        if (visualRoot == null)
        {
            ReleaseMaterials();
            return;
        }

        SpriteRenderer[] renderers =
            visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (Matches(renderers))
            return;

        ReleaseMaterials();
        if (shader == null)
            shader = Resources.Load<Shader>("DRPGStandardHitFlash");
        if (shader == null || !shader.isSupported)
            return;

        foreach (SpriteRenderer renderer in renderers)
        {
            Material original = renderer.sharedMaterial;
            if (!Supports(original))
                continue;
            string shaderName = original.shader.name;
            var pulse = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            pulse.CopyPropertiesFromMaterial(original);
            pulse.SetFloat("_AnkaluteUseLighting",
                shaderName ==
                    "Universal Render Pipeline/2D/Sprite-Lit-Default"
                    ? 1f
                    : 0f);
            pulse.SetFloat("_AnkaluteHitFlash", 0f);
            bindings.Add(new Binding
            {
                Renderer = renderer,
                Original = original,
                Pulse = pulse
            });
        }
    }

    internal int BeginPulse()
    {
        EndPulse(token);
        active = true;
        token++;
        if (token == 0)
            token++;
        foreach (Binding binding in bindings)
        {
            if (binding.Renderer == null || binding.Pulse == null)
                continue;
            binding.Pulse.SetFloat("_AnkaluteHitFlash", 1f);
            binding.Renderer.sharedMaterial = binding.Pulse;
        }
        return token;
    }

    internal void SetPulse(int owner, float amount)
    {
        if (!active || owner != token)
            return;
        foreach (Binding binding in bindings)
            if (binding.Renderer != null && binding.Pulse != null &&
                binding.Renderer.sharedMaterial == binding.Pulse)
                binding.Pulse.SetFloat("_AnkaluteHitFlash", amount);
    }

    internal void EndPulse(int owner)
    {
        if (!active || owner != token)
            return;
        active = false;
        foreach (Binding binding in bindings)
        {
            if (binding.Renderer != null &&
                binding.Renderer.sharedMaterial == binding.Pulse)
                binding.Renderer.sharedMaterial = binding.Original;
            if (binding.Pulse != null)
                binding.Pulse.SetFloat("_AnkaluteHitFlash", 0f);
        }
    }

    private bool Matches(SpriteRenderer[] renderers)
    {
        int eligible = 0;
        foreach (SpriteRenderer renderer in renderers)
        {
            Material material = renderer.sharedMaterial;
            if (!Supports(material))
                continue;
            eligible++;
            bool found = false;
            foreach (Binding binding in bindings)
                if (binding.Renderer == renderer &&
                    binding.Original == material && binding.Pulse != null)
                {
                    found = true;
                    break;
                }
            if (!found)
                return false;
        }
        return eligible == bindings.Count;
    }

    private static bool Supports(Material material)
    {
        if (material == null || material.shader == null)
            return false;
        string name = material.shader.name;
        return name == "Sprites/Default" ||
            name == "Universal Render Pipeline/2D/Sprite-Lit-Default" ||
            name == "Universal Render Pipeline/2D/Sprite-Unlit-Default";
    }

    private void OnDisable() => EndPulse(token);

    private void OnDestroy() => ReleaseMaterials();

    private void ReleaseMaterials()
    {
        EndPulse(token);
        foreach (Binding binding in bindings)
            if (binding.Pulse != null)
            {
                if (Application.isPlaying)
                    Destroy(binding.Pulse);
                else
                    DestroyImmediate(binding.Pulse);
            }
        bindings.Clear();
    }
}
