/*
 * 脚本：CharacterViewSpaceAdapter
 * 功能：可选角色观察适配，缓存实际相机基、转换玩家意图并调整纯视觉子层。
 * 边界：没有Update/设备采样，不移动角色根/碰撞，不改玩法朝向、动作身份或Spine主轨。
 * 协作：CharacterRoot初始化、每帧先Tick再仲裁、仲裁后更新表现翻转并最终释放；场景上下文独占owner。
 */
using UnityEngine;

namespace Ossastra.Characters.ViewSpace
{
    [DisallowMultipleComponent]
    public sealed class CharacterViewSpaceAdapter : MonoBehaviour
    {
        [SerializeField, Tooltip("仅视觉父层；不得指定角色根或包含碰撞/世界投递物的层级。")]
        private Transform visualRoot;
        private Transform character;
        private Quaternion originalLocalRotation;
        private Quaternion presentationRotation;
        private object contextOwner;
        private ICharacterViewContext context;
        private Vector3 right = Vector3.right, forward = Vector3.forward;
        private bool initialized, frameValid, hasPresentation, inputCaptured;
        private float presentationFacing = 1;
        public bool IsEffective => initialized && isActiveAndEnabled && frameValid && ContextAlive;
        public object ContextOwner => contextOwner;
        public ICharacterViewContext Context => ContextAlive ? context : null;
        public float PresentationFacing => presentationFacing;
        public string Diagnostics => !initialized ? "未配置视觉子层" : contextOwner == null ? "未绑定；沿原路径" :
            !IsEffective ? "上下文无效；沿原路径" : (inputCaptured ? "观察捕获输入；" : "正常输入；") + "实际相机基已采样";
        private bool ContextAlive => context != null && (!(context is Object unityObject) || unityObject != null);

        public bool Initialize(Transform root)
        {
            if (initialized) return character == root;
            if (root == null || visualRoot == null || visualRoot == root || !visualRoot.IsChildOf(root)) return false;
            character = root;
            originalLocalRotation = visualRoot.localRotation;
            initialized = true;
            return true;
        }

        public bool Bind(object owner, ICharacterViewContext next)
        {
            if (!initialized || !isActiveAndEnabled || owner == null || next == null ||
                next is Object unityObject && unityObject == null || contextOwner != null && !ReferenceEquals(contextOwner, owner)) return false;
            if (next.GameplayCamera == null) return false;
            if (ReferenceEquals(contextOwner, owner) && ReferenceEquals(context, next)) return true;
            contextOwner = owner;
            context = next;
            frameValid = false;
            hasPresentation = false;
            return true;
        }

        public bool SetInputCapture(object owner, bool captured)
        {
            if (contextOwner == null || !ReferenceEquals(contextOwner, owner)) return false;
            inputCaptured = captured;
            return true;
        }

        /// <summary>Root在唯一仲裁前调用。过渡使用实际观察基；结构模式保留前一合法视觉平面。</summary>
        public void Tick()
        {
            frameValid = false;
            if (!initialized || !isActiveAndEnabled || !ContextAlive || !context.IsValid || context.GameplayCamera == null)
            { RestoreVisual(); return; }
            var nextRight = context.ViewRight;
            var nextForward = context.ViewForward;
            var rotation = context.ViewRotation;
            if (!CharacterViewSpaceMath.IsValidBasis(nextRight, nextForward, rotation)) { RestoreVisual(); return; }
            if (!context.FreezePresentation || !hasPresentation)
            {
                right = nextRight;
                forward = nextForward;
                presentationRotation = rotation;
                hasPresentation = true;
            }
            // 无效上下文/禁用时会还原本地旋转；恢复冻结观察也要重新应用同一缓存平面，不能只恢复方向基。
            visualRoot.rotation = presentationRotation;
            frameValid = true;
        }

        public Vector2 MapPlayerInput(Vector2 input) => IsEffective ? CharacterViewSpaceMath.MapInput(input, right, forward) : input;
        public Vector3 ToPresentation(Vector3 world) => IsEffective ? CharacterViewSpaceMath.ToPresentation(world, right, forward) : world;

        /// <summary>只维护视觉镜像的派生值；角色Orientation仍持有唯一世界方向与原默认翻转。</summary>
        public void ResolvePresentation(Vector3 world, float originalFacing)
        {
            if (!IsEffective) { presentationFacing = originalFacing; return; }
            var local = ToPresentation(world);
            if (Mathf.Abs(local.x) > .01f) presentationFacing = Mathf.Sign(local.x);
        }

        public bool Release(object owner)
        {
            if (contextOwner == null || !ReferenceEquals(contextOwner, owner)) return false;
            Dispose();
            return true;
        }

        public void Suspend() { frameValid = false; RestoreVisual(); }
        public void Dispose()
        {
            Suspend(); contextOwner = null; context = null; inputCaptured = false; hasPresentation = false;
        }
        private void RestoreVisual() { if (initialized && visualRoot != null) visualRoot.localRotation = originalLocalRotation; }
        private void OnDisable() => Suspend();
        private void OnDestroy() => Dispose();
    }
}
