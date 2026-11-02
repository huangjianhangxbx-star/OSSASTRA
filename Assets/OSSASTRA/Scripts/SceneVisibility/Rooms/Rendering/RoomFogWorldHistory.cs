/* 功能：以四份有限世界域保留最近半秒的覆盖变化，供当前相机重新投影渐显/静态退场。
 * 边界：没有屏幕颜色历史，不保存Renderer或业务；拓扑epoch改变立即清除，历史不能授予动态/标签权限。
 * 生命周期：每相机一个小型只读StructuredBuffer，1/6秒抽样；释放/重入完全归所有者。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ossastra.SceneVisibility.Rooms.Rendering
{
    internal sealed class RoomFogWorldHistory : IDisposable
    {
        internal const int Capacity = 4, DomainStride = 52, SeedBase = 4, DomainBase = 44, SnapshotStride = 876;
        readonly List<Vector4[]> snapshots = new List<Vector4[]>(Capacity);
        readonly Vector4[] upload = new Vector4[Capacity * SnapshotStride];
        readonly Vector4[] previousFrame = new Vector4[SnapshotStride];
        ComputeBuffer buffer;
        int epoch, seedFingerprint;
        bool initialized;
        double clockOrigin, lastCapture;
        internal int Count => snapshots.Count;
        internal ComputeBuffer Buffer => buffer;
        internal float Time(double now) => (float)(now - clockOrigin);
        internal long Bytes => buffer == null ? 0 : Capacity * SnapshotStride * 16L;
        internal void Update(RoomMaskFrame frame, double now)
        {
            if (frame.Fog == null || !frame.Fog.Enabled) { Clear(); return; }
            if (!initialized || epoch != frame.VisibilityEpoch || now < lastCapture)
            {
                snapshots.Clear(); initialized = true; epoch = frame.VisibilityEpoch; clockOrigin = lastCapture = now;
                var empty = new Vector4[SnapshotStride]; empty[0].x = -.001f; snapshots.Add(empty);
                snapshots.Add(Capture(frame,0));seedFingerprint=Fingerprint(frame);Fill(frame,Time(now),previousFrame);Upload(); return;
            }
            int nextFingerprint=Fingerprint(frame);bool changed=false;
            if(nextFingerprint!=seedFingerprint)
            {
                // 穿门是离散种子变化；把前一帧覆盖放在事件前沿，避免误认为早在上次1/6秒抽样时就已失权。
                var before=(Vector4[])previousFrame.Clone();before[0].x=Time(now)-.00001f;Append(before);
                Append(Capture(frame,Time(now)));lastCapture=now;seedFingerprint=nextFingerprint;changed=true;
            }
            else if(now-lastCapture>=RoomFogTransitionMath.SampleInterval)
            {Append(Capture(frame,Time(now)));lastCapture=now;changed=true;}
            Fill(frame,Time(now),previousFrame);if(changed)Upload();
        }
        void Append(Vector4[] value){if(snapshots.Count==Capacity)snapshots.RemoveAt(0);snapshots.Add(value);}
        static int Fingerprint(RoomMaskFrame frame)
        {
            unchecked{int hash=frame.ExteriorView?1:0;foreach(var box in frame.SeedVolumes)hash=(hash*397)^box.WorldToLocal.GetHashCode()^box.HalfExtents.GetHashCode();return hash;}
        }
        Vector4[] Capture(RoomMaskFrame frame, float time)
        {
            var values = new Vector4[SnapshotStride];Fill(frame,time,values);return values;
        }
        void Fill(RoomMaskFrame frame,float time,Vector4[] values)
        {
            Array.Clear(values,0,values.Length);
            int count = frame.SightDomains.Length + frame.WindowSightDomains.Length;
            values[0] = new Vector4(time, frame.SeedVolumes.Length, count, frame.ExteriorView ? 1 : 0);
            values[1] = frame.ObserverPosition; values[2] = frame.CameraObserverPosition;
            for (int i = 0; i < frame.SeedVolumes.Length; ++i) StoreVolume(values, SeedBase + i * 5, frame.SeedVolumes[i]);
            for (int i = 0; i < count; ++i)
            {
                bool window = i >= frame.SightDomains.Length;
                var domain = window ? frame.WindowSightDomains[i - frame.SightDomains.Length] : frame.SightDomains[i];
                int start = DomainBase + i * DomainStride;
                values[start] = new Vector4(domain.Planes.Length, domain.TargetVolumes.Length, domain.TargetExterior ? 1 : 0, window ? 1 : 0);
                values[start + 1] = window ? frame.CameraObserverPosition : frame.ObserverPosition;
                for (int p = 0; p < domain.Planes.Length; ++p)
                {
                    var plane = domain.Planes[p]; float length = new Vector3(plane.x, plane.y, plane.z).magnitude;
                    values[start + 2 + p] = plane / length;
                }
                for (int v = 0; v < domain.TargetVolumes.Length; ++v) StoreVolume(values, start + 12 + v * 5, domain.TargetVolumes[v]);
            }
        }
        static void StoreVolume(Vector4[] values, int start, RoomMaskVolume volume)
        {
            for (int row = 0; row < 4; ++row) values[start + row] = volume.WorldToLocal.GetRow(row);
            values[start + 4] = volume.HalfExtents;
        }
        void Upload()
        {
            Array.Clear(upload, 0, upload.Length);
            for (int i = 0; i < snapshots.Count; ++i) Array.Copy(snapshots[i], 0, upload, i * SnapshotStride, SnapshotStride);
            if (buffer == null) buffer = new ComputeBuffer(upload.Length, 16, ComputeBufferType.Structured);
            buffer.SetData(upload);
        }
        internal void Clear() { snapshots.Clear(); initialized = false; buffer?.Release(); buffer = null; }
        public void Dispose() { Clear(); }
    }
}
