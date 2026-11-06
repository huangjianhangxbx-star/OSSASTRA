// 功能：仅给真实Registry源码提供离线identity/事件测试所需的最小Unity外壳，不模拟渲染、销毁或场景API。
using System;
namespace UnityEngine {
 public sealed class Camera { static int next;readonly int id=++next;public int GetInstanceID()=>id;}
 public enum RuntimeInitializeLoadType {SubsystemRegistration}
 [AttributeUsage(AttributeTargets.Method)]public sealed class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType _){} }
}
namespace Ossastra.EnvironmentSimulation {public sealed class EnvironmentFrame {} }
