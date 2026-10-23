#if UNITY_EDITOR
using System;
using UnityEngine;

public static class DevDebugHubInputProbe
{
    public static string Run()
    {
        GameObject ownerObject = new GameObject("DebugHubInputProbeRoot");
        ownerObject.hideFlags = HideFlags.HideAndDontSave;
        ownerObject.SetActive(false);
        try
        {
            GameUIRoot uiRoot = ownerObject.AddComponent<GameUIRoot>();
            DevDebugHubInputRouter router =
                ownerObject.AddComponent<DevDebugHubInputRouter>();
            router.Initialize(uiRoot);

            object competingOwner = new object();
            Assert(uiRoot.ModalCoordinator.TryAcquire(competingOwner),
                "Probe must acquire the competing modal lease");
            Assert(!router.Open(),
                "Debug hub must refuse to open while another modal owns input");
            Assert(ReferenceEquals(
                    uiRoot.ModalCoordinator.ActiveOwner,
                    competingOwner),
                "Rejected open must not steal another modal lease");

            Assert(uiRoot.ModalCoordinator.Release(competingOwner),
                "Probe must release the competing modal lease");
            Assert(router.Open(),
                "Debug hub must open after modal ownership becomes available");
            Assert(uiRoot.ModalCoordinator.IsOwnedBy(router),
                "Open debug hub must own the modal lease");
            Assert(router.IsOpen,
                "Router must expose its open state");

            router.CloseImmediate();
            Assert(!router.IsOpen,
                "Immediate close must clear the open state");
            Assert(uiRoot.ModalCoordinator.ActiveOwner == null,
                "Closing debug hub must release modal ownership");
            return "PASS development debug hub modal input lifecycle";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    private static void Assert(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
#endif
