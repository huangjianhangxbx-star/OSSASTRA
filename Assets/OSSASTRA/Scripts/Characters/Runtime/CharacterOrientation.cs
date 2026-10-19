using UnityEngine;

namespace Ossastra.Characters
{
    // Owns planar orientation only; never moves the body or plays animation.
    [DisallowMultipleComponent]
    public sealed class CharacterOrientation : MonoBehaviour
    {
        public Vector3 Direction { get; private set; } = Vector3.right;
        public float FacingX { get; private set; } = 1;

        public void OrientTowards(Vector3 worldPosition)
        {
            var offset = worldPosition - transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude < .0001f) return;
            Direction = offset.normalized;
            if (Mathf.Abs(Direction.x) > .01f) FacingX = Mathf.Sign(Direction.x);
        }
    }
}
