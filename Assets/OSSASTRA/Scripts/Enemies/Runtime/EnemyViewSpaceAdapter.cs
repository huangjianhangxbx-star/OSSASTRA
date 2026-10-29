/* 脚本：EnemyViewSpaceAdapter
 * 功能：敌根显式推进的可选观察适配，纯视觉平面对齐共同相机，左右取观察水平投影。
 * 边界：不持有玩家输入/Current/Bus，不改根、真实方向、碰撞和动作；近零投影保留本实例方向。
 * 生命周期：owner精确绑定/释放，恢复所拥有的局部旋转；结构模式缓存正常观察平面。
 */
using UnityEngine;
using Ossastra.Characters.ViewSpace;
namespace Ossastra.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyViewSpaceAdapter:MonoBehaviour,ICharacterViewConsumer
    {
        [SerializeField] private Transform visualRoot;
        private Transform entity;
        private Quaternion originalRotation,cachedRotation;
        private Vector3 cachedRight;
        private Vector3 lastWorldDirection=Vector3.right;
        private ICharacterViewContext context;
        private object contextOwner;
        private bool initialized,hasFrame;
        public float Facing {get;private set;}=1;
        public bool IsEffective {get;private set;}
        public object ContextOwner=>contextOwner;
        public bool Initialize(Transform root)
        {
            if(initialized)return entity==root;
            if(root==null||visualRoot==null||visualRoot==root||!visualRoot.IsChildOf(root))return false;
            if(visualRoot.GetComponentsInChildren<Collider>(true).Length>0)return false;
            entity=root;originalRotation=visualRoot.localRotation;initialized=true;return true;
        }
        public bool BindViewContext(object owner,ICharacterViewContext next)
        {
            if(!initialized||!isActiveAndEnabled||owner==null||next==null||next.GameplayCamera==null||
                next is Object obj&&obj==null||contextOwner!=null&&!ReferenceEquals(contextOwner,owner))return false;
            contextOwner=owner;context=next;hasFrame=false;return true;
        }
        public void TickView(Vector3 worldDirection)
        {
            IsEffective=false;
            if(!initialized||!isActiveAndEnabled||context==null||context is Object obj&&obj==null||!context.IsValid)
            {Restore();return;}
            if(!context.FreezePresentation||!hasFrame)
            {
                if(!CharacterViewSpaceMath.IsValidBasis(context.ViewRight,context.ViewForward,context.ViewRotation)){Restore();return;}
                cachedRotation=context.ViewRotation;cachedRight=context.ViewRight;hasFrame=true;
            }
            visualRoot.rotation=cachedRotation;
            worldDirection.y=0;
            if(worldDirection.sqrMagnitude>.0001f)lastWorldDirection=worldDirection.normalized;
            // 停步后仍按新观察角投影末世界显示方向，不能把上次屏幕左右当成世界方向。
            float horizontal=Vector3.Dot(lastWorldDirection,cachedRight);
            if(Mathf.Abs(horizontal)>.01f)Facing=Mathf.Sign(horizontal);
            IsEffective=true;
        }
        public bool ReleaseViewContext(object owner)
        {
            if(contextOwner==null||!ReferenceEquals(contextOwner,owner))return false;
            Restore();contextOwner=null;context=null;hasFrame=false;return true;
        }
        private void Restore(){IsEffective=false;if(initialized&&visualRoot!=null)visualRoot.localRotation=originalRotation;}
        private void OnDisable()=>Restore();
        private void OnDestroy(){Restore();contextOwner=null;context=null;}
    }
}
