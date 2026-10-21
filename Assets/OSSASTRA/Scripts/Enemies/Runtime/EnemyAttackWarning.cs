/* 世界预警适配：LateUpdate只读取敌根真实动作投影与近战空间参数。
 * 使用两个复用LineRenderer，无动作时钟/伤害写入；生命周期显式销毁自有材质和对象。
 * SAMPLE：地面圆为当前追踪预计范围，不承诺锁定落点或命中；无单向光照。
 */
using UnityEngine;
namespace Ossastra.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyRoot))]
    public sealed class EnemyAttackWarning : MonoBehaviour
    {
        private EnemyRoot root;
        private LineRenderer ring, direction;
        private Material material;
        public bool Visible => ring != null && ring.enabled;
        private void Awake()
        {
            root=GetComponent<EnemyRoot>();
            var shader=Shader.Find("Sprites/Default");
            if(shader==null) { enabled=false; return; }
            material=new Material(shader){name="SAMPLE_AttackWarning_Runtime"};
            ring=CreateLine("TrackingRange",49,true); direction=CreateLine("TrackingDirection",2,false);
        }
        private LineRenderer CreateLine(string label,int count,bool loop)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;
            line.positionCount=count;line.loop=loop;line.useWorldSpace=true;line.widthMultiplier=.035f;
            line.startColor=line.endColor=new Color(1,.38f,.2f,.8f);line.enabled=false;return line;
        }
        private void LateUpdate()
        {
            if(ring==null)return;
            var show=root!=null && root.WarningVisible;
            ring.enabled=direction.enabled=show;
            if(!show)return;
            var center=root.WarningCenter;center.y=transform.position.y+.04f;
            for(int i=0;i<ring.positionCount;i++)
            {float angle=i*Mathf.PI*2/(ring.positionCount-1);ring.SetPosition(i,center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*root.WarningRadius);}
            direction.SetPosition(0,new Vector3(transform.position.x,center.y,transform.position.z));direction.SetPosition(1,center);
        }
        private void OnDisable(){if(ring!=null)ring.enabled=false;if(direction!=null)direction.enabled=false;}
        private void OnDestroy()
        {
            if(ring!=null)Destroy(ring.gameObject);if(direction!=null)Destroy(direction.gameObject);
            if(material!=null)Destroy(material);
        }
    }
}
