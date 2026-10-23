#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DRPGBattleSceneBindingProbe
{
    public static string Run()
    {
        GameObject parent = new GameObject("BorrowedOriginalParent");
        GameObject actor = new GameObject("BorrowedActor");
        try
        {
            actor.transform.SetParent(parent.transform, false);
            actor.transform.localPosition = new Vector3(3f, 4f, 5f);
            actor.transform.localScale = new Vector3(2f, 2f, 2f);
            SpriteRenderer renderer = actor.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 17;
            Rigidbody2D body = actor.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.velocity = new Vector2(2f, -1f);
            BattleWorldActorView view = actor.AddComponent<BattleWorldActorView>();
            view.EnsureAnchors();
            int instanceId = actor.GetInstanceID();

            var lease = new BattleBorrowedActorLease(view);
            if (body.simulated)
                throw new InvalidOperationException(
                    "Borrowed battle actors must suspend scene physics while presentation owns their pose.");
            actor.transform.SetParent(null, true);
            actor.transform.position = Vector3.one * 99f;
            actor.transform.localScale = Vector3.one * 9f;
            actor.SetActive(false);
            renderer.sortingOrder = 999;
            lease.Dispose();
            lease.Dispose();

            if (actor.GetInstanceID() != instanceId ||
                actor.transform.parent != parent.transform ||
                actor.transform.localPosition != new Vector3(3f, 4f, 5f) ||
                actor.transform.localScale != new Vector3(2f, 2f, 2f) ||
                !actor.activeSelf || renderer.sortingOrder != 17 ||
                !body.simulated || body.velocity != new Vector2(2f, -1f))
            {
                throw new InvalidOperationException(
                    "Borrowed actor release must restore the same scene instance exactly once.");
            }

            GameObject contextObject = new GameObject("Context");
            GameObject partyObject = new GameObject("Party");
            GameObject cameraObject = new GameObject("Camera");
            GameObject originObject = new GameObject("Origin");
            GameObject enemiesObject = new GameObject("Enemies");
            try
            {
                var party = partyObject.AddComponent<DRPGNodePartyRoot>();
                Camera camera = cameraObject.AddComponent<Camera>();
                partyObject.transform.position = new Vector3(12f, 3f, 0f);
                cameraObject.transform.position = new Vector3(40f, 8f, -10f);
                var context = contextObject.AddComponent<DRPGNodeBattleContext>();
                context.ConfigureForRuntime("probe", DRPGNodeBattleMode.ExplorationEncounter,
                    party, camera, originObject.transform, enemiesObject.transform,
                    new DRPGNodeBattleBounds(-100f, 100f), 1f, 1f);
                Vector3 before = partyObject.transform.position;
                context.CaptureCurrentSceneComposition();
                if (partyObject.transform.position != before)
                    throw new InvalidOperationException(
                        "Entering battle must not pull the party root to the camera.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(contextObject);
                UnityEngine.Object.DestroyImmediate(partyObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(originObject);
                UnityEngine.Object.DestroyImmediate(enemiesObject);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(parent);
        }
        return "PASS scene camera origin and borrowed actor restoration";
    }
}
#endif
