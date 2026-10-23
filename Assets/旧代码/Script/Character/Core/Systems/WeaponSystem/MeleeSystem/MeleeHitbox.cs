using UnityEngine;

public class MeleeHitbox : MonoBehaviour
{
    [Header("识别")]
    [SerializeField] private string hitboxId;

    [Header("判定形状")]
    [SerializeField] private Collider2D hitCollider;

    public string HitboxId => hitboxId;
    public Collider2D HitCollider => hitCollider;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (hitCollider == null)
        {
            hitCollider =
                GetComponent<Collider2D>();
        }

        if (hitCollider != null)
        {
            hitCollider.isTrigger = true;
        }
    }
#endif
}