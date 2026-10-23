using UnityEngine;

public static class DevDebugHubRuntime
{
    private static DevDebugRegistry registry = new DevDebugRegistry();

    public static DevDebugRegistry Registry => registry;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        registry = new DevDebugRegistry();
    }
}
