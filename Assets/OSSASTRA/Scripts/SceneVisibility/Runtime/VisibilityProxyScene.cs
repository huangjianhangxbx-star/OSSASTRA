/* 功能：为明确登记的装饰Sprite提供有界AABB查询Box，物理世界与主游戏场完全隔离。
 * 边界：不扫描场景、不挂原物体父节点、不建Renderer/Actor/Rigidbody、不读fade颜色，不推进物理。
 * 协作：Root登记来源、消费GetCollider并按changed重组TransientProxies；dirty时显式同步一次Transform。
 * 生命周期：惰性创建独立LocalPhysicsMode.Physics3D Scene；关闭Box后只异步卸载自己Scene。
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch=System.Diagnostics.Stopwatch;
using Object=UnityEngine.Object;

namespace Ossastra.SceneVisibility
{
    public sealed class VisibilityProxyScene : IDisposable
    {
        public const int MaximumSprites=64;
        private const float MinimumAxis=.001f;
        private sealed class Entry
        {
            public SpriteRenderer Source;
            public BoxCollider Box;
            public Bounds Bounds;
        }
        private struct Input
        {
            public SpriteRenderer Source;
            public Bounds Bounds;
        }
        private readonly List<Entry> entries=new List<Entry>(MaximumSprites);
        private readonly Dictionary<SpriteRenderer,Entry> bySource=new Dictionary<SpriteRenderer,Entry>(MaximumSprites);
        private readonly HashSet<SpriteRenderer> desired=new HashSet<SpriteRenderer>();
        private readonly List<Input> inputs=new List<Input>(MaximumSprites);
        private Scene scene;
        private bool dirty,disposed;

        public string SceneName { get; }="__KT_VIS01_QueryPhysics_"+Guid.NewGuid().ToString("N");
        public int OwnedColliderCount => entries.Count;
        public long SyncCount { get; private set; }
        public double LastSyncMilliseconds { get; private set; }
        public string LastError { get; private set; }
        public AsyncOperation UnloadOperation { get; private set; }

        public void Synchronize(SpriteRenderer[] registered,out bool changed)
        {
            changed=false;
            if(disposed)throw new ObjectDisposedException(nameof(VisibilityProxyScene));
            if(!Application.isPlaying)throw new InvalidOperationException("查询物理Scene只在Play建立，不改变编辑场。 ");
            if(registered!=null&&registered.Length>MaximumSprites)
                throw new ArgumentException("显式Sprite查询登记最多64项。",nameof(registered));

            // 先完整检查本次输入，再改自身映射；无效bounds不会留下半次登记。
            desired.Clear();inputs.Clear();
            if(registered!=null)for(int i=0;i<registered.Length;++i)
            {
                var source=registered[i];
                if(source==null||source.sprite==null||!source.enabled||!source.gameObject.activeInHierarchy)continue;
                if(!source.gameObject.scene.IsValid()||!source.gameObject.scene.isLoaded)
                    throw new ArgumentException("Sprite必须属于已加载的明确来源Scene。",nameof(registered));
                if(!desired.Add(source))continue;
                var bounds=source.bounds;
                if(!Finite(bounds.center)||!Finite(bounds.size)||bounds.size.x<0||bounds.size.y<0||bounds.size.z<0)
                    throw new ArgumentException("Sprite world bounds必须为有限非负AABB。",nameof(registered));
                if(bounds.size.x==0&&bounds.size.y==0&&bounds.size.z==0){desired.Remove(source);continue;}
                inputs.Add(new Input{Source=source,Bounds=bounds});
            }

            for(int i=entries.Count-1;i>=0;--i)
            {
                var entry=entries[i];
                if(entry.Source!=null&&desired.Contains(entry.Source)&&entry.Box!=null&&entry.Box.gameObject.scene==scene)continue;
                Remove(entry);entries.RemoveAt(i);changed=true;
            }
            if(inputs.Count>0)EnsureScene();
            for(int i=0;i<inputs.Count;++i)
            {
                var input=inputs[i];
                if(!bySource.TryGetValue(input.Source,out var entry))
                {
                    entry=Create(input.Source,input.Bounds);
                    entries.Add(entry);bySource.Add(input.Source,entry);changed=true;
                }
                else if(!entry.Bounds.Equals(input.Bounds)||!entry.Box.enabled)
                { Apply(entry,input.Bounds);changed=true; }
            }
            dirty|=changed;LastError=null;
        }

        private void EnsureScene()
        {
            if(scene.IsValid()&&scene.isLoaded)return;
            scene=SceneManager.CreateScene(SceneName,new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            if(!scene.IsValid()||!scene.isLoaded||!scene.GetPhysicsScene().IsValid()||scene.GetPhysicsScene()==Physics.defaultPhysicsScene)
            {
                LastError="创建独立Physics3D查询Scene失败。";
                Dispose();
                throw new InvalidOperationException(LastError);
            }
        }

        private Entry Create(SpriteRenderer source,Bounds bounds)
        {
            GameObject go=null;
            try
            {
                // 不使用HideAndDontSave/HideInHierarchy：这些标记可能将Collider内部移出physics scene。
                // Move发生在AddComponent<Collider>之前，因此该Box从未进入主PhysicsScene。
                go=new GameObject("KT_VIS01_QueryBox_"+source.GetInstanceID());go.SetActive(false);
                SceneManager.MoveGameObjectToScene(go,scene);
                var box=go.AddComponent<BoxCollider>();box.isTrigger=true;
                var entry=new Entry{Source=source,Box=box};Apply(entry,bounds);go.SetActive(true);return entry;
            }
            catch
            {
                if(go!=null)DestroyOwned(go);
                throw;
            }
        }

        private static void Apply(Entry entry,Bounds bounds)
        {
            var transform=entry.Box.transform;
            // 无父物体、世界identity姿态，直接采用Renderer实际world AABB。
            transform.SetPositionAndRotation(bounds.center,Quaternion.identity);transform.localScale=Vector3.one;
            entry.Box.center=Vector3.zero;
            var size=bounds.size;
            entry.Box.size=new Vector3(Mathf.Max(MinimumAxis,size.x),Mathf.Max(MinimumAxis,size.y),Mathf.Max(MinimumAxis,size.z));
            entry.Box.isTrigger=true;entry.Box.enabled=true;entry.Bounds=bounds;
        }

        private void Remove(Entry entry)
        {
            // Unity已销毁的Source仍保留managed key，可按原引用从Dictionary精准删除。
            bySource.Remove(entry.Source);
            if(entry.Box==null)return;
            entry.Box.enabled=false;entry.Box.gameObject.SetActive(false);DestroyOwned(entry.Box.gameObject);
        }

        public BoxCollider GetCollider(SpriteRenderer source)
        {
            if(disposed||source==null||!bySource.TryGetValue(source,out var entry)||entry.Box==null||!entry.Box.enabled||
                !entry.Box.gameObject.activeInHierarchy||entry.Box.gameObject.scene!=scene)return null;
            return entry.Box;
        }

        public bool SyncTransformsIfDirty()
        {
            if(disposed||!dirty){LastSyncMilliseconds=0;return false;}
            long started=Stopwatch.GetTimestamp();
            try{Physics.SyncTransforms();}
            finally{LastSyncMilliseconds=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;}
            dirty=false;++SyncCount;return true;
        }

        public void Dispose()
        {
            if(disposed)return;disposed=true;
            // Async卸载完成前也立即不可查询；没有Actor或刚体，绝不Simulate这个world。
            for(int i=0;i<entries.Count;++i)if(entries[i].Box!=null)
            {entries[i].Box.enabled=false;entries[i].Box.gameObject.SetActive(false);}
            entries.Clear();bySource.Clear();desired.Clear();inputs.Clear();dirty=false;
            if(scene.IsValid()&&scene.isLoaded)
            {
                try
                {
                    UnloadOperation=SceneManager.UnloadSceneAsync(scene);
                    if(UnloadOperation==null)LastError="自身查询Scene异步卸载未返回operation："+SceneName;
                }
                catch(Exception error)
                {
                    // 例如编辑器已退出Play；只关闭自己Box，保留精确SceneName供root审计，不清理其它Scene。
                    LastError="自身查询Scene卸载异常："+SceneName+" / "+error.Message;
                }
            }
        }

        private static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static void DestroyOwned(GameObject go)
        {
            if(Application.isPlaying)Object.Destroy(go);else Object.DestroyImmediate(go);
        }
    }
}
