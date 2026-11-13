/*
 * 脚本：SideViewEffectMesh
 * 功能：生成可复用的横版刀弧、爆点、两侧条带、短屑和防御形状。
 * 边界：仅创建自有网格。以 +X 为自然前向，原点为配方枢轴。
 */
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Combat
{
    internal static class SideViewEffectMesh
    {
        internal static Mesh Build(SideViewEffectLayerShape shape)
        {
            var vertices = new List<Vector3>(256);
            var colors = new List<Color>(256);
            switch (shape)
            {
                case SideViewEffectLayerShape.Crescent:
                    Crescent(.71f, .25f, -83f, 78f, .98f, -.08f);
                    Crescent(.82f, .046f, -66f, 70f, .52f, -.08f);
                    Crescent(.58f, .047f, -76f, 7f, .53f, -.05f);
                    break;
                case SideViewEffectLayerShape.SlashAccent:
                    Streak(new Vector2(-.24f, -.19f), new Vector2(.56f, .30f), .04f, .9f);
                    break;
                case SideViewEffectLayerShape.ContactFlash:
                    Flash(.74f);
                    Streak(new Vector2(-.19f, -.1f), new Vector2(.54f, .25f), .11f, 1f);
                    Streak(new Vector2(.18f, -.37f), new Vector2(-.12f, .49f), .075f, .9f);
                    break;
                case SideViewEffectLayerShape.DirectionalStrips:
                    Streak(new Vector2(.07f, .02f), new Vector2(1.05f, .22f), .062f, 1f);
                    Streak(new Vector2(-.04f, -.02f), new Vector2(-.71f, -.19f), .052f, .98f);
                    Streak(new Vector2(.16f, -.04f), new Vector2(.82f, -.27f), .041f, .87f);
                    Streak(new Vector2(-.15f, .04f), new Vector2(-.60f, .22f), .04f, .82f);
                    Streak(new Vector2(.17f, .10f), new Vector2(.72f, .41f), .045f, .89f);
                    Streak(new Vector2(.30f, -.23f), new Vector2(.68f, -.48f), .03f, .71f);
                    break;
                case SideViewEffectLayerShape.DirectionalMotes:
                    Streak(new Vector2(.23f, .16f), new Vector2(.41f, .36f), .059f, .91f);
                    Streak(new Vector2(.47f, .24f), new Vector2(.62f, .33f), .044f, .84f);
                    Streak(new Vector2(.42f, -.18f), new Vector2(.52f, -.34f), .052f, .84f);
                    Streak(new Vector2(.57f, .08f), new Vector2(.79f, .14f), .046f, .89f);
                    Streak(new Vector2(-.38f, -.16f), new Vector2(-.49f, -.24f), .034f, .74f);
                    break;
                case SideViewEffectLayerShape.GuardRim:
                    Crescent(.58f, .15f, -89f, 89f, .99f, 0);
                    Crescent(.73f, .04f, -76f, 76f, .73f, 0);
                    Streak(new Vector2(.39f, .08f), new Vector2(.63f, -.43f), .055f, .89f);
                    break;
                case SideViewEffectLayerShape.EvadeStreaks:
                    Streak(new Vector2(-.58f, -.16f), new Vector2(.55f, -.23f), .071f, .65f);
                    Streak(new Vector2(-.23f, .17f), new Vector2(.78f, .13f), .076f, .83f);
                    break;
                case SideViewEffectLayerShape.SurfaceChips:
                    Streak(new Vector2(.03f, .07f), new Vector2(.29f, .51f), .072f, .87f);
                    Streak(new Vector2(.07f, -.03f), new Vector2(.42f, -.30f), .068f, .85f);
                    Streak(new Vector2(.15f, .12f), new Vector2(.58f, .21f), .06f, .88f);
                    Streak(new Vector2(.31f, -.07f), new Vector2(.61f, -.16f), .047f, .80f);
                    break;
            }
            var mesh = new Mesh { name = "SideViewCombat_" + shape, hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetTriangles(indices, 0);
            return mesh;

            void Triangle(Vector2 a, Vector2 b, Vector2 c, float opacityA, float opacityB, float opacityC)
            {
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                colors.Add(new Color(1, 1, 1, opacityA));
                colors.Add(new Color(1, 1, 1, opacityB));
                colors.Add(new Color(1, 1, 1, opacityC));
            }

            void Streak(Vector2 root, Vector2 tip, float width, float opacity)
            {
                var d = (tip - root).normalized;
                var across = new Vector2(-d.y, d.x) * width;
                var front = Vector2.Lerp(root, tip, .25f);
                var back = Vector2.Lerp(root, tip, .42f);
                Triangle(root, front + across, back, .28f * opacity, .16f * opacity, opacity);
                Triangle(front + across, tip, back, .16f * opacity, 0f, opacity);
                Triangle(root, back, front - across, .28f * opacity, opacity, .16f * opacity);
                Triangle(front - across, back, tip, .16f * opacity, opacity, 0f);
            }

            void Flash(float opacity)
            {
                const int sections = 16;
                for (int i = 0; i < sections; i++)
                {
                    var a = Unit(i * (360f / sections));
                    var b = Unit((i + 1) * (360f / sections));
                    Triangle(Vector2.zero, a * .29f, b * .29f, opacity, 0f, 0f);
                }
            }

            void Crescent(float radius, float width, float bottom, float top, float opacity, float tilt)
            {
                const int sections = 20;
                for (int i = 0; i < sections; i++)
                {
                    float t = (float)i / sections;
                    float u = (float)(i + 1) / sections;
                    var a = Unit(Mathf.Lerp(bottom, top, t));
                    var b = Unit(Mathf.Lerp(bottom, top, u));
                    float wa = width * Mathf.Sin(Mathf.PI * t) * Mathf.Lerp(.64f, 1f, t);
                    float wb = width * Mathf.Sin(Mathf.PI * u) * Mathf.Lerp(.64f, 1f, u);
                    var ao = a * (radius + tilt * t);
                    var bo = b * (radius + tilt * u);
                    var am = ao - a * wa * .30f;
                    var bm = bo - b * wb * .30f;
                    var ai = ao - a * wa;
                    var bi = bo - b * wb;
                    float alphaA = opacity * Mathf.SmoothStep(0f, 1f, Mathf.Sin(Mathf.PI * t));
                    float alphaB = opacity * Mathf.SmoothStep(0f, 1f, Mathf.Sin(Mathf.PI * u));
                    Triangle(ao, am, bm, alphaA * .24f, alphaA, alphaB);
                    Triangle(ao, bm, bo, alphaA * .24f, alphaB, alphaB * .24f);
                    Triangle(am, ai, bi, alphaA, 0f, 0f);
                    Triangle(am, bi, bm, alphaA, 0f, alphaB);
                }
            }
        }

        private static Vector2 Unit(float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}

