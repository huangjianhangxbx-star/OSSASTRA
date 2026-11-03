/* 功能：让明确登记的新观察区叶片读取同相机环境快照，在原localRotation上作有界轻摆。
 * 边界：不查场景、不推进时钟、不接旧billboard；仅允许自身后代的无碰撞/无动画叶支系，不改位置、缩放、根或角色。
 * 生命周期：保存叶片原姿态；配置改绑、Original、环境登记释放、禁用与销毁时立即精确恢复。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ossastra.EnvironmentSimulation
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class EnvironmentVegetationWind : MonoBehaviour
    {
        [Header("明确环境根；只读其登记相机的快照")]
        public EnvironmentRoot environment;
        [Header("本物体下的新无碰撞叶片；不登记旧billboard、角色或物理支系")]
        public Transform[] foliage = Array.Empty<Transform>();
        [Range(0, 15)] public float maxBendDegrees = 5;
        public string Status { get; private set; } = "等待明确叶片与环境";

        sealed class LeafLease
        {
            internal Transform leaf;
            internal Quaternion rotation;
        }
        readonly List<LeafLease> leases = new List<LeafLease>();
        EnvironmentRoot leasedEnvironment;
        Camera leasedCamera;
        bool subscribed;

        void OnEnable()
        {
            if (!subscribed)
            {
                EnvironmentCameraRegistry.Published += OnEnvironmentPublished;
                EnvironmentCameraRegistry.Released += OnEnvironmentReleased;
                subscribed = true;
            }
            RefreshNow();
        }
        void OnDisable()
        {
            if (subscribed)
            {
                EnvironmentCameraRegistry.Published -= OnEnvironmentPublished;
                EnvironmentCameraRegistry.Released -= OnEnvironmentReleased;
                subscribed = false;
            }
            Restore(); Status = "已归还叶片原姿态";
        }
        void OnDestroy() { OnDisable(); }
        void OnValidate() { if (isActiveAndEnabled) RefreshNow(); }
        void Update() { RefreshNow(); }
        void OnEnvironmentPublished(Camera camera)
        {
            if (ReferenceEquals(camera, leasedCamera) || (environment != null && camera == environment.gameplayCamera))
                RefreshNow();
        }
        void OnEnvironmentReleased(int cameraId)
        {
            if (ReferenceEquals(leasedCamera, null) || leasedCamera.GetInstanceID() != cameraId) return;
            Restore(); Status = "环境登记已释放；叶片原姿态已恢复";
        }

        /// <summary>只消费现有快照；供正常Update、环境发布及Native验收使用，不另建计时器。</summary>
        public void RefreshNow()
        {
            if (!isActiveAndEnabled || environment == null || !environment.isActiveAndEnabled ||
                environment.gameObject.scene != gameObject.scene || environment.gameplayCamera == null ||
                !EnvironmentCameraRegistry.TryGet(environment.gameplayCamera, out var frame) || !frame.Enabled)
            { Restore(); Status = "等待同相机有效环境；保持叶片原姿态"; return; }
            if (float.IsNaN(maxBendDegrees) || float.IsInfinity(maxBendDegrees))
            { Restore(); Status = "摆幅无效；已恢复叶片"; return; }
            if (!ValidateLeaves(out string reason))
            { Restore(); Status = reason; return; }

            Camera camera = environment.gameplayCamera;
            if (!MatchesLease(camera))
            {
                Restore(); leasedEnvironment = environment; leasedCamera = camera;
                foreach (Transform leaf in foliage) leases.Add(new LeafLease { leaf = leaf, rotation = leaf.localRotation });
            }
            float amplitude = Mathf.Clamp(maxBendDegrees, 0, 15) * Mathf.Clamp01(frame.WindSpeed);
            Vector3 worldAxis = new Vector3(frame.WindDirection.y, 0, -frame.WindDirection.x);
            if (amplitude <= 0 || worldAxis.sqrMagnitude < .000001f)
            {
                foreach (var lease in leases) lease.leaf.localRotation = lease.rotation;
                Status = "无风；保持叶片原姿态"; return;
            }
            worldAxis.Normalize();
            // 风速控制摆幅；相位仅随同一快照秒数推进，变风时不把累计秒数重新乘频率而突然跳相。
            const double frequency = 1.65;
            for (int i = 0; i < leases.Count; i++)
            {
                var lease = leases[i];
                float angle = (float)Math.Sin(frame.Seconds * frequency + i * .73) * amplitude;
                Vector3 localAxis = lease.leaf.parent != null ? lease.leaf.parent.InverseTransformDirection(worldAxis) : worldAxis;
                lease.leaf.localRotation = Quaternion.AngleAxis(angle, localAxis) * lease.rotation;
            }
            Status = "同相机环境风：" + leases.Count + " 片叶；最大 " + amplitude.ToString("0.0") + "°";
        }

        bool MatchesLease(Camera camera)
        {
            if (leasedEnvironment != environment || !ReferenceEquals(leasedCamera, camera) || leases.Count != foliage.Length) return false;
            for (int i = 0; i < foliage.Length; i++) if (leases[i].leaf != foliage[i]) return false;
            return true;
        }
        bool ValidateLeaves(out string reason)
        {
            reason = "没有明确登记本物体下的叶片";
            if (foliage == null || foliage.Length == 0) return false;
            for (int i = 0; i < foliage.Length; i++)
            {
                Transform leaf = foliage[i];
                if (leaf == null || leaf == transform || !leaf.IsChildOf(transform) ||
                    leaf == environment.transform || environment.transform.IsChildOf(leaf))
                { reason = "叶片引用越过本物体支系或指向环境根；拒绝摆动"; return false; }
                for (int j = 0; j < i; j++) if (foliage[j] == leaf)
                { reason = "叶片重复登记；拒绝叠加旋转"; return false; }
                // 扫描仅限明确叶支系与祖先，不遍历场景。任何物理/动画所有者都优先于这份显示租约。
                if (leaf.GetComponentInChildren<Collider>(true) != null || leaf.GetComponentInParent<Collider>() != null ||
                    leaf.GetComponentInChildren<Collider2D>(true) != null || leaf.GetComponentInParent<Collider2D>() != null ||
                    leaf.GetComponentInChildren<Animator>(true) != null || leaf.GetComponentInParent<Animator>() != null ||
                    leaf.GetComponentInChildren<Rigidbody>(true) != null || leaf.GetComponentInParent<Rigidbody>() != null ||
                    leaf.GetComponentInChildren<Rigidbody2D>(true) != null || leaf.GetComponentInParent<Rigidbody2D>() != null)
                { reason = "叶支系含碰撞、物理或动画所有者；拒绝摆动"; return false; }
            }
            return true;
        }
        void Restore()
        {
            foreach (var lease in leases) if (lease.leaf != null) lease.leaf.localRotation = lease.rotation;
            leases.Clear(); leasedEnvironment = null; leasedCamera = null;
        }
    }
}
