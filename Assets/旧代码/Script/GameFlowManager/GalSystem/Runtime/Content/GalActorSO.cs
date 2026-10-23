using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GalPortraitVariant
{
    [SerializeField] private string key;
    [SerializeField] private Sprite sprite;

    public string Key => key ?? string.Empty;
    public Sprite Sprite => sprite;

    public GalPortraitVariant(string key, Sprite sprite)
    {
        this.key = key;
        this.sprite = sprite;
    }
}

[CreateAssetMenu(
    fileName = "GalActor_",
    menuName = "Project ANOIR/Gal/Actor")]
public sealed class GalActorSO : ScriptableObject
{
    [SerializeField] private string actorId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite defaultPortrait;
    [SerializeField] private List<GalPortraitVariant> portraitVariants = new();

    public string ActorId => actorId ?? string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public Sprite DefaultPortrait => defaultPortrait;

    public Sprite ResolvePortrait(string variantKey)
    {
        if (!string.IsNullOrWhiteSpace(variantKey) && portraitVariants != null)
        {
            for (int i = 0; i < portraitVariants.Count; i++)
            {
                GalPortraitVariant variant = portraitVariants[i];
                if (variant != null &&
                    string.Equals(variant.Key, variantKey, StringComparison.Ordinal))
                {
                    return variant.Sprite;
                }
            }
        }

        return defaultPortrait;
    }
}
