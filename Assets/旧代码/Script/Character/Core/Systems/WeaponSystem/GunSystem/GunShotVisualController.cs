using UnityEngine;

public class GunShotVisualController : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject tracerPrefab;
    [SerializeField] private GameObject defaultImpactPrefab;
    [SerializeField] private GameObject enemyImpactPrefab;

    [Header("Lifetime")]
    [SerializeField] private float muzzleFlashLifetime = 0.5f;
    [SerializeField] private float tracerLifetime = 0.05f;
    [SerializeField] private float impactLifetime = 0.6f;

    [Header("Muzzle Flash Transform")]
    [SerializeField] private Vector3 muzzleFlashRotationOffsetEuler = new Vector3(0f, 90f, 0f);
    [SerializeField] private float muzzleFlashScale = 0.5f;


    [Header("Sorting")]
    [SerializeField] private string muzzleFlashSortingLayer = "VFX";
    [SerializeField] private int muzzleFlashOrderInLayer = 100;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    public void PlayShot(
        Vector2 origin,
        Vector2 endPoint,
        RaycastHit2D hit,
        Transform muzzlePoint
    )
    {
        Vector2 shootDirection = endPoint - origin;

        if (shootDirection.sqrMagnitude <= 0.0001f)
            shootDirection = Vector2.right;

        shootDirection.Normalize();

        PlayMuzzleFlash(muzzlePoint, shootDirection);

        if (tracerPrefab != null)
        {
            PlayTracer(origin, endPoint);
        }

        if (hit.collider != null)
        {
            bool hitEnemy = hit.collider.GetComponentInParent<EnemyRoot>() != null;
            PlayImpact(hit.point, hit.normal, hitEnemy);
        }
    }

    public void PlayMuzzleFlash(Transform muzzlePoint, Vector2 shootDirection)
    {
        if (muzzleFlashPrefab == null)
        {
            Debug.LogWarning("[GunShotVisualController] Muzzle Flash Prefab is null", this);
            return;
        }

        if (muzzlePoint == null)
        {
            Debug.LogWarning("[GunShotVisualController] Muzzle Point is null", this);
            return;
        }

        if (shootDirection.sqrMagnitude <= 0.0001f)
            shootDirection = Vector2.right;

        shootDirection.Normalize();

        float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;

        Quaternion aimRotation = Quaternion.Euler(0f, 0f, angle);
        Quaternion offsetRotation = Quaternion.Euler(muzzleFlashRotationOffsetEuler);
        Quaternion finalRotation = aimRotation * offsetRotation;

        Vector3 spawnPosition = muzzlePoint.position;

        // 如果仍然被遮挡，可以试试 -0.1 或 0.1
        spawnPosition.z = -0.1f;

        GameObject instance = Instantiate(
            muzzleFlashPrefab,
            spawnPosition,
            finalRotation
        );

        instance.transform.localScale = Vector3.one * muzzleFlashScale;

        ApplyParticleSorting(instance);

        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in particles)
        {
            ps.gameObject.SetActive(true);
            ps.Clear(true);
            ps.Play(true);
        }

        if (debugLog)
        {
            Debug.Log(
                $"[GunShotVisualController] Spawn MuzzleFlash at {spawnPosition}, angle={angle}, particles={particles.Length}",
                this
            );
        }

        Destroy(instance, muzzleFlashLifetime);
    }

    private void PlayTracer(Vector2 origin, Vector2 endPoint)
    {
        if (tracerPrefab == null)
            return;

        GameObject instance = Instantiate(
            tracerPrefab,
            origin,
            Quaternion.identity
        );

        LineRenderer lineRenderer = instance.GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }

        Destroy(instance, tracerLifetime);
    }

    public void PlayImpact(Vector3 position, Vector2 normal, bool hitEnemy)
    {
        GameObject prefab = hitEnemy ? enemyImpactPrefab : defaultImpactPrefab;

        if (prefab == null)
            return;

        float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;

        GameObject instance = Instantiate(
            prefab,
            position,
            Quaternion.Euler(0f, 0f, angle)
        );

        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem ps in particles)
        {
            ps.gameObject.SetActive(true);
            ps.Clear(true);
            ps.Play(true);
        }

        if (debugLog)
        {
            Debug.Log(
                $"[GunShotVisualController] Spawn Impact at {position}, hitEnemy={hitEnemy}, particles={particles.Length}",
                this
            );
        }

        Destroy(instance, impactLifetime);
    }

    private void ApplyParticleSorting(GameObject instance)
    {
        ParticleSystemRenderer[] renderers =
            instance.GetComponentsInChildren<ParticleSystemRenderer>(true);

        foreach (ParticleSystemRenderer renderer in renderers)
        {
            renderer.sortingLayerName = muzzleFlashSortingLayer;
            renderer.sortingOrder = muzzleFlashOrderInLayer;
        }
    }





}