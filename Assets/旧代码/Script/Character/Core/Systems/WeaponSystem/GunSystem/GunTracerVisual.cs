using UnityEngine;

public class GunTracerVisual : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;

    public void Initialize(Vector2 origin, Vector2 endPoint, float lifetime)
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, origin);
            lineRenderer.SetPosition(1, endPoint);
        }

        Destroy(gameObject, lifetime);
    }
}