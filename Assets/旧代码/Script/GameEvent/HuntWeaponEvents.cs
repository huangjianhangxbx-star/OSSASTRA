using UnityEngine;

public struct WeaponFiredEvent
{
    public GameObject owner;
    public Vector3 position;
    public Vector2 direction;
    public WeaponAudioProfileSO audioProfile;
    public bool isPlayer;
}

public struct WeaponReloadEvent
{
    public GameObject owner;
    public Vector3 position;
    public WeaponAudioProfileSO audioProfile;
    public WeaponReloadPhase phase;
}

public struct WeaponEmptyEvent
{
    public GameObject owner;
    public Vector3 position;
    public WeaponAudioProfileSO audioProfile;
    public bool isPlayer;
}

public enum WeaponReloadPhase
{
    Start,
    InsertMagazine,
    Charge,
    End
}