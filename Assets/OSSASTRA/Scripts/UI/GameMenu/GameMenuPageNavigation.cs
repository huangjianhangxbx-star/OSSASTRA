// 处理菜单页内焦点、选择与确认。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.UI.GameMenu
{
    /// <summary>One page's directional focus and optional subpage, modal, and map commands.</summary>
    public sealed class GameMenuPageNavigation : MonoBehaviour
    {
        private sealed class Target
        {
            public RectTransform Rect;
            public GameObject Highlight;
            public Action Selected;
            public Action Confirmed;
            public UnityEngine.UI.ScrollRect Scroll;
        }

        private readonly List<Target> targets = new List<Target>();
        private Target current;

        internal Func<Vector2Int, bool> Direction { get; set; }
        internal Func<bool> Back { get; set; }
        internal Func<int, bool> ChangeContext { get; set; }
        internal Func<string, bool> Extra { get; set; }
        internal Action SessionReset { get; set; }
        internal Action ConfirmWithoutTarget { get; set; }

        internal Action Add(RectTransform rect, GameObject highlight, Action selected, Action confirmed = null,
            UnityEngine.UI.ScrollRect scroll = null)
        {
            var target = new Target
            {
                Rect = rect,
                Highlight = highlight,
                Selected = selected,
                Confirmed = confirmed,
                Scroll = scroll
            };
            highlight.SetActive(false);
            targets.Add(target);
            return () => Focus(target);
        }

        public void RefreshFocus()
        {
            if (Available(current))
            {
                current.Highlight.SetActive(true);
                return;
            }
            Focus(null);
            foreach (var target in targets)
            {
                if (!Available(target)) continue;
                Focus(target);
                return;
            }
        }

        public bool Step(Vector2Int direction)
        {
            if (direction == Vector2Int.zero) return false;
            if (Direction != null && Direction(direction)) return true;
            if (!Available(current))
            {
                RefreshFocus();
                return current != null;
            }

            var from = Center(current.Rect);
            var forward = ((Vector2)direction).normalized;
            Target nearest = null;
            var best = float.PositiveInfinity;
            foreach (var candidate in targets)
            {
                if (candidate == current || !Available(candidate)) continue;
                var delta = Center(candidate.Rect) - from;
                var ahead = Vector2.Dot(delta, forward);
                if (ahead < 1f) continue;
                var sideways = Mathf.Abs(delta.x * forward.y - delta.y * forward.x);
                var score = ahead + sideways * 2.25f;
                if (score >= best) continue;
                best = score;
                nearest = candidate;
            }

            if (nearest == null) return false;
            Focus(nearest);
            return true;
        }

        public bool Submit()
        {
            if (Available(current))
            {
                current.Confirmed?.Invoke();
                return true;
            }
            if (ConfirmWithoutTarget == null) return false;
            ConfirmWithoutTarget();
            return true;
        }

        public bool Cancel() => Back != null && Back();
        public bool PreviousContext() => ChangeContext != null && ChangeContext(-1);
        public bool NextContext() => ChangeContext != null && ChangeContext(1);
        public bool Auxiliary(string action) => Extra != null && Extra(action);
        public void ResetSession() => SessionReset?.Invoke();

        private static bool Available(Target target)
            => target != null && target.Rect != null && target.Rect.gameObject.activeInHierarchy;

        private Vector2 Center(RectTransform rect)
            => transform.InverseTransformPoint(rect.TransformPoint(rect.rect.center));

        private void Focus(Target target)
        {
            if (target == current) return;
            if (current?.Highlight != null) current.Highlight.SetActive(false);
            current = target;
            if (target == null) return;
            target.Highlight.SetActive(true);
            target.Selected?.Invoke();
            Reveal(target);
        }

        private static void Reveal(Target target)
        {
            var scroll = target.Scroll;
            if (scroll == null || scroll.viewport == null || scroll.content == null) return;

            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target.Rect);
            var window = scroll.viewport.rect;
            var overflow = scroll.content.rect.height - window.height;
            if (overflow <= 0f) return;

            var adjust = bounds.max.y > window.yMax ? window.yMax - bounds.max.y
                : bounds.min.y < window.yMin ? window.yMin - bounds.min.y : 0f;
            if (Mathf.Abs(adjust) < .01f) return;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - adjust / overflow);
        }
    }
}
