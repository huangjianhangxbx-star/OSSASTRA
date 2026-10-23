using System.Collections.Generic;
using UnityEngine;

public class CharacterCombatGeometryProvider : MonoBehaviour
{
    [SerializeField]
    private List<CombatHitAnchor> anchors =
        new List<CombatHitAnchor>();

    private readonly Dictionary<string, CombatHitAnchor>
        anchorMap =
            new Dictionary<string, CombatHitAnchor>();

    private void Awake()
    {
        RebuildCache();
    }

    public void RebuildCache()
    {
        anchorMap.Clear();

        for (int i = 0; i < anchors.Count; i++)
        {
            CombatHitAnchor anchor = anchors[i];

            if (anchor == null ||
                string.IsNullOrWhiteSpace(anchor.AnchorId))
            {
                continue;
            }

            anchorMap[anchor.AnchorId] = anchor;
        }
    }

    public bool TryGetCharacterAnchor(
        string anchorId,
        out CombatHitAnchor anchor)
    {
        if (string.IsNullOrWhiteSpace(anchorId))
        {
            anchor = null;
            return false;
        }

        return anchorMap.TryGetValue(
            anchorId,
            out anchor
        );
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (anchors == null)
        {
            anchors =
                new List<CombatHitAnchor>();
        }
    }
#endif
}