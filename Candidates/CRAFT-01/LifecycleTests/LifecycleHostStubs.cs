/* 来源：原Candidates/COM01/SessionTests/HostStubs.cs的独占诊断副本。
 * 仅补真实CommerceService编译所需Unity/UI元数据外壳；Ledger/Session/Economy/保存与Codec全为原源码。
 * 不代签Unity销毁语义；用inventory=null显式驱动与destroyed-object-null相同的宿主条件分支。
 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterCore;
using Ossastra.Inventory;
namespace UnityEngine
{
    public sealed class SerializeField : Attribute { }
    public sealed class MinAttribute : Attribute { public MinAttribute(float min) { } }
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int n) { } }
    public sealed class DisallowMultipleComponent : Attribute { }
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string text) { } }
    public class MonoBehaviour
    {
        public bool enabled=true; public bool isActiveAndEnabled=>enabled;
        public Transform transform=new Transform(); public GameObject gameObject=new GameObject();
        public Dictionary<Type,object> components=new Dictionary<Type,object>();
        public T GetComponent<T>() where T:class => components.TryGetValue(typeof(T),out var item)?(T)item:null;
        public T GetComponentInChildren<T>() where T:class=>GetComponent<T>();
        public static T FindObjectOfType<T>() where T:class=>null;
    }
    public class Transform { public Vector3 position; public bool IsChildOf(Transform other)=>ReferenceEquals(this,other); }
    public class GameObject { public Scene scene=new Scene(); }
    public class Scene { public string path="fixture-map",name="fixture-map"; }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public float magnitude=>(float)Math.Sqrt(x*x+y*y);public static explicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y); }
    public struct Vector3
    {
        public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);public Vector3 normalized=>magnitude==0?new Vector3():this*(1/magnitude);
        public static Vector3 up=>new Vector3(0,1,0);public static Vector3 down=>new Vector3(0,-1,0);
        public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator*(Vector3 a,float f)=>new Vector3(a.x*f,a.y*f,a.z*f);
    }
    public static class Mathf {public static float Abs(float n)=>Math.Abs(n);}
    public static class Debug { public static readonly List<string> Errors=new List<string>(); public static void LogError(object value,object context){Errors.Add(value.ToString());Console.WriteLine("ACTUAL_DIAGNOSTIC "+value);} }
    public class Camera {public static Camera main; public Ray ScreenPointToRay(Vector2 p)=>new Ray();public Vector3 WorldToScreenPoint(Vector3 p)=>p;}
    public static class Screen {public static int width=1000,height=700;}
    public static class Application {public static string persistentDataPath="UNUSED_MEMORY_STORE_FIXTURE";}
    public static class Time {public static float deltaTime=.016f,unscaledTime;}
    public class Sprite { }
    public struct Ray {}
    public enum QueryTriggerInteraction { Ignore }
    public class Collider {public Transform transform=new Transform();public T GetComponentInParent<T>() where T:class=>null;}
    public struct RaycastHit {public Vector3 normal,point;public float distance;public Collider collider;}
    public static class Physics
    {
        public static RaycastHit[] RaycastAll(Ray ray,float d,int layer,QueryTriggerInteraction query)=>Array.Empty<RaycastHit>();
        public static RaycastHit[] RaycastAll(Vector3 p,Vector3 d,float length,int layer,QueryTriggerInteraction query)=>new[]{new RaycastHit{normal=Vector3.up,collider=new Collider()}};
    }
}
namespace Spine.Unity {public class SkeletonAnimation {public Skeleton Skeleton;}public class Skeleton {public object Data;}}
namespace Ossastra.Combat { public class CombatVitals {} public class CombatEngagementSession {public bool Engaged;}public static class CombatDeliveryQueue {public static bool Clear(UnityEngine.Vector3 a,UnityEngine.Vector3 b)=>true;} }
namespace Ossastra.Characters
{
    public class RigDefinition {public object rigProfile;}
    public sealed class CharacterRoot : UnityEngine.MonoBehaviour
    {
        public string ActorId;public bool IsAlive=true,IsSimulationSuspended;public bool CanSimulateIntent=>IsAlive&&!IsSimulationSuspended;
        public RigDefinition Definition;public event Action<CharacterRoot> Died;public void Die(){IsAlive=false;Died?.Invoke(this);}
    }
    public sealed class CharacterControlCoordinator {public List<CharacterRoot> Characters=new List<CharacterRoot>();public CharacterRoot Current;public int ControlRevision;}
    public sealed class CharacterEquipment
    {
        public EquipmentState State; public EquipmentDefinitionSO RightDefinition,LeftDefinition; public bool CanChangeEquipment=true;
        public ICharacterEquipmentInventory Bound;
        public void BindInventory(ICharacterEquipmentInventory slots){Bound=slots;State.BindSlots(slots);}
    }
    public interface ICharacterEquipmentInventory:IEquipmentSlots
    {EquipmentDefinitionSO RightDefinition{get;} EquipmentDefinitionSO LeftDefinition{get;} EquipmentDefinitionSO[] AvailableOffhands{get;}bool TryEquip(EquipmentDefinitionSO definition);}
    public class SpinePresentation {public void Freeze(object data,object profile){}}
    public sealed class EquipmentDefinitionSO {public string definitionId;public bool mainHand;public OffhandKind offhandKind;public SpinePresentation spinePresentation;}
    public sealed class ItemDefinitionSO
    {
        public string definitionId,displayName,description,category;public int width=1,height=1,maxStack=99,unitWeightGrams,sellValue;public object rarity="Gray";public UnityEngine.Sprite icon; public EquipmentDefinitionSO equipment;
        public InventoryDefinition FreezeInventory()=>new InventoryDefinition(definitionId,width,height,equipment==null?maxStack:1,equipment==null?InventoryHand.None:equipment.mainHand?InventoryHand.Right:InventoryHand.Left);
    }
}
