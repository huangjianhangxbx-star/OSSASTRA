#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGNodePartyPlayModeProbe
{
    public static string Run()
    {
        var rootObject = new GameObject("Probe Party Root");
        try
        {
            DRPGNodePartyRoot root = rootObject.AddComponent<DRPGNodePartyRoot>();
            root.ConfigureForRuntime(2f, -10f, 10f);
            DRPGScenePartyMember first = CreateMember(root.transform, "hero", -1f);
            DRPGScenePartyMember second = CreateMember(root.transform, "friend", 1f);
            root.RebuildFromChildren();

            float separation = second.transform.localPosition.x -
                first.transform.localPosition.x;
            float firstScaleX = first.transform.localScale.x;
            root.Motor.Step(-1f, 0.5f);

            if (Math.Abs(root.transform.position.x + 1f) > 0.001f ||
                Math.Abs((second.transform.localPosition.x -
                    first.transform.localPosition.x) - separation) > 0.001f ||
                Math.Abs(first.transform.localScale.x - firstScaleX) > 0.001f)
            {
                throw new InvalidOperationException(
                    "Exploration movement must translate only the party root.");
            }

            root.SetExplorationEnabled(false);
            root.Motor.Step(1f, 1f);
            if (Math.Abs(root.transform.position.x + 1f) > 0.001f)
                throw new InvalidOperationException("Battle must freeze party-root movement.");

            if (!root.TrySwap("hero", "friend") ||
                root.Formation.Order[1] != "hero")
            {
                throw new InvalidOperationException(
                    "Any two members, including protagonist, must be swappable.");
            }

            return "PASS node party root movement and ordering";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rootObject);
        }
    }

    private static DRPGScenePartyMember CreateMember(
        Transform parent,
        string id,
        float localX)
    {
        var child = new GameObject(id);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = new Vector3(localX, 0f, 0f);
        DRPGScenePartyMember member = child.AddComponent<DRPGScenePartyMember>();
        member.ConfigureForRuntime(id, null, null);
        return member;
    }
}
#endif
