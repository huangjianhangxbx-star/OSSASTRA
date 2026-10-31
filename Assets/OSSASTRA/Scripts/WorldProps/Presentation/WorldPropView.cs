/* 脚本：WorldPropView
 * 功能：消费真实耐久回执，短时显示耐久条并晃动独立视觉子层，二维/三维共用。
 * 边界：不读取输入、不扣血发物、不移动逻辑根或碰撞；无独立Update和私有音源。
 * 协作：WorldPropsRoot只推进活跃反馈；条形网格首击懒建，借用材质并以MPB着色。
 * 生命周期：耗尽后拒绝迟到受击，禁用时还原视觉TRS并释放自有网格/对象，不复显模型。
 */
using UnityEngine;

namespace Ossastra.WorldProps
{
    [DisallowMultipleComponent]
    public sealed class WorldPropView : MonoBehaviour
    {
        public Transform visualRoot;
        [Min(0)]
        public float barHeight = 1.5f;
        [Min(.05f)]
        public float barWidth = 1f;
        [Min(.05f)]
        public float feedbackSeconds = 1.5f;
        public Material barMaterial;
        public bool IsAnimating => !depleted && remaining > 0 && isActiveAndEnabled;

        Transform boundVisual, barRoot, fill;
        Mesh ownedMesh;
        Vector3 originalPosition, originalScale;
        Quaternion originalRotation;
        bool depleted;
        float remaining, age, fraction;
        const float ShakeSeconds = .18f;
        /// <summary>由初始化/受控恢复显式重置显示，不改变领域生命或耗尽状态。</summary>
        public void ResetView(bool isDepleted)
        {
            RestoreVisual();
            BindVisual();
            depleted = isDepleted;
            remaining = age = 0;
            if (barRoot != null)
                barRoot.gameObject.SetActive(false);
            if (depleted && boundVisual != null)
                boundVisual.gameObject.SetActive(false);
        }

        public void ShowHit(int current, int maximum)
        {
            if (depleted || !isActiveAndEnabled || maximum <= 0)
                return;
            BindVisual();
            age = 0;
            remaining = Finite(feedbackSeconds) ? Mathf.Clamp(feedbackSeconds, .05f, 30f) : 1.5f;
            fraction = Mathf.Clamp01((float)current / maximum);
            EnsureBar();
            if (barRoot != null)
            {
                barRoot.gameObject.SetActive(true);
                UpdateFill();
            }
        }

        /// <summary>预算淘汰或根暂停时结束本次反馈；保留耗尽保护，不复活视觉或改变耐久。</summary>
        public void StopFeedback()
        {
            remaining = age = 0;
            RestoreVisual();
            if (barRoot != null)
                barRoot.gameObject.SetActive(false);
        }

        public void ShowDepleted()
        {
            depleted = true;
            remaining = 0;
            RestoreVisual();
            if (barRoot != null)
                barRoot.gameObject.SetActive(false);
            if (boundVisual != null)
                boundVisual.gameObject.SetActive(false);
        }

        /// <summary>由根的活跃队列调用；到期即休眠，不搜索相机、不扫场景。</summary>
        public void Tick(float delta, Camera camera)
        {
            if (!IsAnimating || !Finite(delta) || delta < 0)
                return;
            age += delta;
            remaining = Mathf.Max(0, remaining - delta);
            if (boundVisual != null)
                boundVisual.localRotation = originalRotation * Quaternion.AngleAxis(age < ShakeSeconds ? Mathf.Sin(age * 85f) * 6f * (1f - age / ShakeSeconds) : 0, Vector3.forward);
            if (barRoot != null)
            {
                barRoot.position = transform.position + Vector3.up * (Finite(barHeight) ? Mathf.Clamp(barHeight, 0, 50) : 1.5f);
                if (camera != null)
                    barRoot.rotation = camera.transform.rotation;
                if (remaining == 0)
                    barRoot.gameObject.SetActive(false);
            }

            if (remaining == 0)
                RestoreVisual();
        }

        void BindVisual()
        {
            // 只接受实体自己的子层，错误配置绝不能让受击反馈摇动逻辑根或外部物件。
            if (boundVisual != null || visualRoot == null || visualRoot == transform || !visualRoot.IsChildOf(transform))
                return;
            boundVisual = visualRoot;
            originalPosition = boundVisual.localPosition;
            originalRotation = boundVisual.localRotation;
            originalScale = boundVisual.localScale;
        }

        void EnsureBar()
        {
            if (barRoot != null || barMaterial == null)
                return;
            ownedMesh = new Mesh
            {
                name = "WorldProp durability quad"
            };
            ownedMesh.vertices = new[]
            {
                new Vector3(-.5f, -.5f),
                new Vector3(.5f, -.5f),
                new Vector3(-.5f, .5f),
                new Vector3(.5f, .5f)
            };
            ownedMesh.uv = new[]
            {
                Vector2.zero,
                Vector2.right,
                Vector2.up,
                Vector2.one
            };
            ownedMesh.colors = new[]
            {
                Color.white,
                Color.white,
                Color.white,
                Color.white
            };
            ownedMesh.triangles = new[]
            {
                0,
                2,
                1,
                2,
                3,
                1
            };
            ownedMesh.RecalculateBounds();
            barRoot = new GameObject("WorldPropDurability").transform;
            barRoot.SetParent(transform, false);
            barRoot.position = transform.position + Vector3.up * (Finite(barHeight) ? Mathf.Clamp(barHeight, 0, 50) : 1.5f);
            var back = MakeQuad("Back", new Color(.04f, .05f, .06f, .9f));
            back.localScale = new Vector3(Width, .085f, 1);
            fill = MakeQuad("Fill", new Color(.91f, .66f, .28f, 1));
            UpdateFill();
        }

        float Width => Finite(barWidth) ? Mathf.Clamp(barWidth, .05f, 10f) : 1f;

        void UpdateFill()
        {
            fill.localScale = new Vector3(Width * fraction, .055f, 1);
            fill.localPosition = new Vector3(-Width * (1 - fraction) * .5f, 0, -.002f);
        }

        Transform MakeQuad(string label, Color color)
        {
            // 不用CreatePrimitive，条不会短暂生成任何碰撞体；两条共用本组件的单一自有Mesh。
            var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(barRoot, false);
            go.GetComponent<MeshFilter>().sharedMesh = ownedMesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = barMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
            return go.transform;
        }

        void RestoreVisual()
        {
            if (boundVisual == null)
                return;
            boundVisual.localPosition = originalPosition;
            boundVisual.localRotation = originalRotation;
            boundVisual.localScale = originalScale;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void Release()
        {
            remaining = 0;
            RestoreVisual();
            if (barRoot != null)
            {
                barRoot.gameObject.SetActive(false);
                DestroyOwned(barRoot.gameObject);
            }

            barRoot = fill = null;
            if (ownedMesh != null)
                DestroyOwned(ownedMesh);
            ownedMesh = null;
        }

        static void DestroyOwned(Object value)
        {
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }

        void OnDisable()
        {
            Release();
        }

        void OnDestroy()
        {
            Release();
        }
    }
}
