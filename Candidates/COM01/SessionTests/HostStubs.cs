/* 功能：只为真实Session纯宿主夹具提供Unity位置/生命周期与角色可用性，不能代签Editor运行。 */
using System;
using System.Collections.Generic;
using Ossastra.CharacterCore;
using Ossastra.Inventory;
namespace UnityEngine
{
    public sealed class SerializeField : Attribute { }
    public sealed class MinAttribute : Attribute { public MinAttribute(float min) { } }
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int n) { } }
    public class MonoBehaviour
    {
        public bool enabled=true; public bool isActiveAndEnabled=>enabled;
        public Transform transform=new Transform(); public GameObject gameObject=new GameObject();
        public Dictionary<Type,object> components=new Dictionary<Type,object>();
        public T GetComponent<T>() where T:class => components.TryGetValue(typeof(T),out var item)?(T)item:null;
        public T GetComponentInChildren<T>() where T:class=>GetComponent<T>();
        public static T FindObjectOfType<T>() where T:class=>null;
    }
    public class Transform { public Vector3 position; }
    public class GameObject { public Scene scene=new Scene(); }
    public class Scene { public string path="fixture-map",name="fixture-map"; }
    public struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public float magnitude=>(float)Math.Sqrt(x*x+y*y); }
    public struct Vector3
    {
        public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 up=>new Vector3(0,1,0);public static Vector3 down=>new Vector3(0,-1,0);
        public static Vector3 operator+(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator-(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator*(Vector3 a,float f)=>new Vector3(a.x*f,a.y*f,a.z*f);
    }
    public static class Mathf {public static float Abs(float n)=>Math.Abs(n);}
    public static class Debug { public static void LogError(object value,object context)=>Console.WriteLine("EXPECTED_OR_UNEXPECTED_DIAGNOSTIC "+value); }
    public class Camera {public static Camera main; public Ray ScreenPointToRay(Vector2 p)=>new Ray();}
    public struct Ray {}
    public enum QueryTriggerInteraction { Ignore }
    public class Collider {public T GetComponentInParent<T>() where T:class=>null;}
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
        public string definitionId,displayName;public int width=1,height=1,maxStack=99; public EquipmentDefinitionSO equipment;
        public InventoryDefinition FreezeInventory()=>new InventoryDefinition(definitionId,width,height,maxStack);
    }
}
