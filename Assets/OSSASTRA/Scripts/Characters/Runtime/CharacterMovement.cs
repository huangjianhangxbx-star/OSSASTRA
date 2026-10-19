using System;
using UnityEngine;

namespace Ossastra.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CharacterMovement : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float speed = 3;
        [SerializeField, Min(.01f)] private float groundStick = 2;
        private CharacterController body;
        private CharacterDataBus dataBus;
        public bool IsMoving { get; private set; }
        public Vector3 Velocity { get; private set; }

        public void Initialize(CharacterDataBus dataBus)
        {
            if (dataBus == null) throw new ArgumentNullException(nameof(dataBus));
            this.dataBus = dataBus;
            body = GetComponent<CharacterController>();
        }

        public void Tick(bool allowed, float delta)
        {
            IsMoving = false;
            Velocity = Vector3.zero;
            if (dataBus == null || body == null || !body.enabled || delta <= 0) return;
            var move = allowed ? Vector2.ClampMagnitude(dataBus.Input.move, 1) : Vector2.zero;
            var before = transform.position;
            body.Move((new Vector3(move.x, 0, move.y) * speed + Vector3.down * groundStick) * delta);
            var displacement = transform.position - before;
            displacement.y = 0;
            Velocity = displacement / delta;
            IsMoving = displacement.sqrMagnitude > .000001f;
        }

        public void Teleport(Vector3 position)
        {
            if (body == null) return;
            var wasEnabled = body.enabled;
            body.enabled = false;
            transform.position = position;
            body.enabled = wasEnabled;
            IsMoving = false;
            Velocity = Vector3.zero;
        }
    }
}
