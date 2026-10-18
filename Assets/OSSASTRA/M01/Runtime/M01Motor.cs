using UnityEngine;

namespace Ossastra.M01
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class M01Motor : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float speed = 3;
        [SerializeField, Min(.01f)] private float groundStick = 2;
        private CharacterController body;
        public Vector3 Aim { get; private set; } = Vector3.right;
        public bool Moving { get; private set; }
        public float FacingX { get; private set; } = 1;

        private void Awake() { body = GetComponent<CharacterController>(); }

        public void AimAt(Vector3 point)
        {
            var offset = point - transform.position;
            offset.y = 0;
            if (offset.sqrMagnitude < .0001f) return;
            Aim = offset.normalized;
            if (Mathf.Abs(Aim.x) > .01f) FacingX = Mathf.Sign(Aim.x);
        }

        public void Tick(Vector2 input, bool allowed, float delta)
        {
            if (body == null || !body.enabled) return;
            var move = allowed ? Vector2.ClampMagnitude(input, 1) : Vector2.zero;
            var before = transform.position;
            body.Move((new Vector3(move.x, 0, move.y) * speed + Vector3.down * groundStick) * delta);
            var displacement = transform.position - before;
            displacement.y = 0;
            Moving = displacement.sqrMagnitude > .000001f;
        }

        public void Teleport(Vector3 position)
        {
            if (body == null) return;
            var wasEnabled = body.enabled;
            body.enabled = false;
            transform.position = position;
            body.enabled = wasEnabled;
            Moving = false;
        }
    }
}
