/* Game 视图调试覆盖，仅读取实际游戏查询、足底和回执。 */
using System;
using UnityEngine;

namespace Ossastra.Development
{
    public sealed partial class AttackLabTrace
    {
        private readonly Vector3[] trail = new Vector3[120];
        private int trailWrite, trailCount;
        private CharacterController body;
        private Camera viewCamera;
        private AttackLabEvent latestQuery;
        private readonly AttackLabEvent[] visibleQueries = new AttackLabEvent[8];
        private readonly Vector3[] visibleContacts = new Vector3[8];
        private string observedAttack = "";

        public void SetObservedAttack(string id)
        {
            observedAttack = id?.Replace("-", "") ?? "";
        }

        private void ClearOverlay()
        {
            trailWrite = trailCount = 0;
            observedAttack = "";
            latestQuery = null;
            body = actor.GetComponent<CharacterController>();
            viewCamera = Camera.main;
        }

        private Vector3 Foot()
        {
            if (!body || !body.enabled) return actor.transform.position;
            var bounds = body.bounds;
            return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        private void SampleFoot()
        {
            if (!ShowTrail) return;
            var foot = Foot();
            if (trailCount > 0 && (foot - trail[(trailWrite + trail.Length - 1) % trail.Length]).sqrMagnitude < .0004f) return;
            trail[trailWrite] = foot;
            trailWrite = (trailWrite + 1) % trail.Length;
            trailCount = Mathf.Min(trailCount + 1, trail.Length);
        }

        private void OnGUI()
        {
            if (!Recording || Event.current.type != EventType.Repaint || !(ShowGeometry || ShowTrail || ShowScale)) return;
            if (!viewCamera || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
            if (!viewCamera) return;
            var color = GUI.color;
            var matrix = GUI.matrix;
            var foot = Frozen && events.Count > 0 && events[events.Count - 1].trial == trial ? events[events.Count - 1].foot : Foot();
            if (ShowScale)
            {
                GUI.color = Color.white;
                Line(foot, foot + Vector3.right);
                Line(foot, foot + Vector3.up);
                Caption(foot + Vector3.right, "1 m");
                Caption(foot + Vector3.up, "1 m");
            }
            if (ShowTrail)
            {
                GUI.color = new Color(.3f, .98f, .9f);
                int first = (trailWrite - trailCount + trail.Length) % trail.Length;
                for (int i = 1; i < trailCount; i++) Line(trail[(first+i-1)%trail.Length], trail[(first+i)%trail.Length]);
                Cross(foot, .1f);
            }
            if (ShowGeometry)
            {
                string key = observedAttack;
                if (string.IsNullOrEmpty(key) && latestQuery != null) key = string.IsNullOrEmpty(latestQuery.parent) ? latestQuery.action : latestQuery.parent;
                int drawn = 0, marked = 0;
                for (int i = events.Count - 1; i >= 0 && !string.IsNullOrEmpty(key) && drawn < visibleQueries.Length; i--)
                {
                    var e = events[i];
                    if (e.trial != trial) break;
                    if (e.action != key && e.parent != key) continue;
                    if (!string.IsNullOrEmpty(e.contactQuality) && marked < visibleContacts.Length && !HasContact(e.contact, marked))
                    {
                        GUI.color = Color.yellow;
                        visibleContacts[marked++] = e.contact;
                        Cross(e.contact, .09f);
                        Caption(e.contact, e.contactQuality);
                    }
                    if (string.IsNullOrEmpty(e.queryType) || HasQuery(e, drawn)) continue;
                    visibleQueries[drawn] = e;
                    GUI.color = new Color(1f, .48f, .17f, drawn == 0 ? 1f : .45f);
                    Circle(e.start, e.radius);
                    if ((e.end - e.start).sqrMagnitude > .000001f)
                    {
                        Circle(e.end, e.radius);
                        var normal = Vector3.Cross(Vector3.forward, (e.end-e.start).normalized) * e.radius;
                        Line(e.start + normal, e.end + normal);
                        Line(e.start - normal, e.end - normal);
                    }
                    Line(e.start, e.start + e.direction * e.radius);
                    if (e.hasSector && e.angle < 360f)
                    {
                        GUI.color = new Color(1f, .85f, .27f);
                        var edge = e.direction.normalized * e.radius;
                        Line(e.sectorOrigin, e.sectorOrigin + Quaternion.AngleAxis(e.angle*.5f, Vector3.forward) * edge);
                        Line(e.sectorOrigin, e.sectorOrigin + Quaternion.AngleAxis(-e.angle*.5f, Vector3.forward) * edge);
                    }
                    if (drawn == 0) Caption(e.start + Vector3.up * e.radius, e.queryType + (e.hasSector ? " · 粗筛 / 角度" : ""));
                    drawn++;
                }
            }
            GUI.matrix = matrix;
            GUI.color = color;
        }

        private bool HasContact(Vector3 point, int count)
        {
            for (int i = 0; i < count; i++)
                if ((visibleContacts[i] - point).sqrMagnitude < .0001f) return true;
            return false;
        }

        private bool HasQuery(AttackLabEvent query, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var drawn = visibleQueries[i];
                if (drawn.queryType == query.queryType && drawn.start == query.start && drawn.end == query.end &&
                    drawn.direction == query.direction && drawn.radius == query.radius && drawn.angle == query.angle) return true;
            }
            return false;
        }

        private void Cross(Vector3 p, float radius)
        {
            Line(p - Vector3.right * radius, p + Vector3.right * radius);
            Line(p - Vector3.up * radius, p + Vector3.up * radius);
        }

        private void Circle(Vector3 p, float radius)
        {
            var previous = p + Vector3.right * radius;
            for (int i = 1; i <= 32; i++)
            {
                float a = i * Mathf.PI / 16f;
                var next = p + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
                Line(previous, next);
                previous = next;
            }
        }

        private Vector2 ScreenPoint(Vector3 point)
        {
            return new Vector2(point.x, Screen.height - point.y);
        }

        private void Caption(Vector3 world, string label)
        {
            var p = viewCamera.WorldToScreenPoint(world);
            if (p.z <= 0) return;
            var pos = ScreenPoint(p);
            GUI.Label(new Rect(pos.x+3, pos.y-18, 170, 20), label);
        }

        private void Line(Vector3 from, Vector3 to)
        {
            var a = viewCamera.WorldToScreenPoint(from);
            var b = viewCamera.WorldToScreenPoint(to);
            if (a.z <= 0 || b.z <= 0) return;
            var start = ScreenPoint(a);
            var delta = ScreenPoint(b) - start;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y, delta.magnitude, 1.4f), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
    }
}
