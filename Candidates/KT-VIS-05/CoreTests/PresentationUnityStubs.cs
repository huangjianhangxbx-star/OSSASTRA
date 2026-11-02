// 仅隔离 Unity 对象引用/假空值/Transform 采样；测试不把这些替身当作渲染或Unity生命周期验收。
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object a, Object b)
        {
            if (ReferenceEquals(a, null)) return ReferenceEquals(b, null) || b.Destroyed;
            if (ReferenceEquals(b, null)) return a.Destroyed;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
    }
    public class Transform : Object { public Vector3 position; }
    public class Component : Object
    {
        public GameObject gameObject = new GameObject();
        public T GetComponent<T>() where T:Component => gameObject.GetComponent<T>();
    }
    public class Renderer : Component { }
    public class MonoBehaviour : Component { public bool isActiveAndEnabled = true; }
    public class GameObject : Object
    {
        readonly Dictionary<Type,Component> components = new Dictionary<Type,Component>();
        public T GetComponent<T>() where T:Component => components.TryGetValue(typeof(T),out var value) ? (T)value : null;
        public T AddComponent<T>() where T:Component,new() { var c=new T();c.gameObject=this;components[typeof(T)]=c;return c; }
    }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
}
