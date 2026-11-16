/*
 * A car can be called from either landing. It moves its support collider only;
 * CharacterMovement carries each actor that retains contact with that collider.
 */
using Ossastra.Characters;
using SliceActor = Ossastra.Characters.CharacterRoot;
using Ossastra.SideViewWorld;
using System.Collections;
using UnityEngine;

namespace Ossastra.SideView.Slice
{
    [DisallowMultipleComponent]
    public sealed class SVSliceElevator : SVSliceWorldInteractable
    {
        public CharacterControlCoordinator party;
        public Transform car;
        public Transform upperStop;
        public Transform lowerStop;
        public SideViewTransitionGate lowerGate;
        public SideViewTransitionArrival lowerArrival;
        [Min(.1f)] public float metresPerSecond = 3.8f;

        private bool atLower;
        private bool moving;
        private bool visitedLower;
        private Vector3 upperConsolePosition;
        public bool AtLower => atLower;
        public bool Moving => moving;

        public override string Prompt => moving ? string.Empty : atLower ? "启动升降机 · 上行" : "启动升降机 · 下行";

        private void Awake()
        {
            upperConsolePosition = transform.position;
            if (upperStop != null) PlaceCar(upperStop.position);
            SetGate();
        }

        private void Start()
        {
            if (lowerArrival != null && lowerArrival.Arrived && lowerStop != null) SetStation(true);
        }

        protected override bool CanUse(SliceActor actor)
        {
            return !moving && party != null && party.Current == actor &&
                car != null && Mathf.Abs(actor.transform.position.x - car.position.x) <= 1.65f &&
                Mathf.Abs(actor.transform.position.y - car.position.y) <= .45f && Available();
        }

        public override void Use(SliceActor actor)
        {
            if (IsAccessible(actor)) Call(!atLower);
        }

        public bool CanCall(bool lower, SliceActor actor)
        {
            return actor != null && party != null && party.Current == actor && atLower != lower && Available();
        }

        public void Call(bool lower)
        {
            if (!Available() || atLower == lower) return;
            StartCoroutine(Travel(lower));
        }

        private bool Available()
            => !moving && car != null && upperStop != null && lowerStop != null &&
                (SideViewTransitionRuntime.Instance == null || !SideViewTransitionRuntime.Instance.Busy);

        private IEnumerator Travel(bool lower)
        {
            moving = true;
            var from = car.position;
            var destination = (lower ? lowerStop : upperStop).position;
            var distance = Vector3.Distance(from, destination);
            var seconds = distance / Mathf.Max(.1f, metresPerSecond);
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                // End the first frame before moving the car, so an arriving passenger
                // can establish CharacterController ground contact.
                yield return null;
                elapsed = Mathf.Min(seconds, elapsed + Time.deltaTime);
                PlaceCar(Vector3.Lerp(from, destination, Mathf.SmoothStep(0f, 1f, elapsed / seconds)));
            }
            SetStation(lower);
            moving = false;
        }

        private void SetStation(bool lower)
        {
            atLower = lower;
            visitedLower |= lower;
            var stop = lower ? lowerStop.position : upperStop.position;
            PlaceCar(stop);
            transform.position = upperConsolePosition + stop - upperStop.position;
            SetGate();
        }

        private void PlaceCar(Vector3 position)
        {
            if (car == null || car.position == position) return;
            car.position = position;
            Physics.SyncTransforms();
        }

        private void SetGate()
        {
            if (lowerGate != null && lowerGate.TryGetComponent<BoxCollider>(out var trigger))
                trigger.enabled = visitedLower;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            moving = false;
            if (car != null && upperStop != null && lowerStop != null) SetStation(atLower);
        }
    }
}