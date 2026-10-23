using UnityEngine;

public struct BulletImpactEvent
{
    public Vector3 position;
    public Vector2 normal;
    public GameObject hitObject;
    public SurfaceType surfaceType;
    public bool hitEnemy;
    public WeaponAudioProfileSO audioProfile;
}

public struct EnemyDiedEvent
{
    public GameObject enemy;
    public Vector3 position;
}

public struct PlayerDamagedEvent
{
    public GameObject player;
    public Vector3 position;
    public float damage;
}

public enum SurfaceType
{
    Default,
    Flesh,
    Metal,
    Concrete,
    Wood,
    Glass,
    Water
}