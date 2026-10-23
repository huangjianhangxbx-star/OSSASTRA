using UnityEngine;

public class CombatHitAnchor : MonoBehaviour
{
    [SerializeField]
    private string anchorId;

    public string AnchorId => anchorId;

    public Vector2 WorldPosition =>
        transform.position;
}