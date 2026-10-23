using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporarily grants battle presentation control of the existing scene camera.
/// </summary>
public sealed class BattleSceneCameraLease : IDisposable
{
    private readonly Camera camera;
    private readonly Vector3 position;
    private readonly Quaternion rotation;
    private readonly float fieldOfView;
    private readonly bool orthographic;
    private readonly float orthographicSize;
    private readonly bool cameraEnabled;
    private readonly MonoBehaviour[] drivers;
    private readonly bool[] driverStates;
    private bool disposed;

    public BattleSceneCameraLease(
        Camera sceneCamera,
        IReadOnlyList<MonoBehaviour> explorationDrivers)
    {
        camera = sceneCamera != null
            ? sceneCamera
            : throw new ArgumentNullException(nameof(sceneCamera));
        position = camera.transform.position;
        rotation = camera.transform.rotation;
        fieldOfView = camera.fieldOfView;
        orthographic = camera.orthographic;
        orthographicSize = camera.orthographicSize;
        cameraEnabled = camera.enabled;

        int count = explorationDrivers != null ? explorationDrivers.Count : 0;
        drivers = new MonoBehaviour[count];
        driverStates = new bool[count];
        for (int i = 0; i < count; i++)
        {
            drivers[i] = explorationDrivers[i];
            if (drivers[i] == null)
                continue;
            driverStates[i] = drivers[i].enabled;
            drivers[i].enabled = false;
        }
        camera.enabled = true;
    }

    public Camera Camera => camera;

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        for (int i = 0; i < drivers.Length; i++)
        {
            if (drivers[i] != null)
                drivers[i].enabled = driverStates[i];
        }
        if (camera == null)
            return;
        camera.transform.SetPositionAndRotation(position, rotation);
        camera.fieldOfView = fieldOfView;
        camera.orthographic = orthographic;
        camera.orthographicSize = orthographicSize;
        camera.enabled = cameraEnabled;
    }
}
