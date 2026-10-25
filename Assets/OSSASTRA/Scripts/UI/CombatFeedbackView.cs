/* 脚本：CombatFeedbackView
 * 功能：消费实际释放/命中/闪避/盾挡/治疗事实，保留短时世界反馈与既有事实计数；音效交统一音频适配器。
 * 边界：不读攻击按键、不接受动作、不执行命中；拒绝请求没有释放反馈。
 * 协作：根ActionObserved及命中收据为事实源；场景销毁解除订阅，临时线条统一回收。
 */
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Ossastra.Characters;
using Ossastra.CharacterCore;
using Ossastra.Combat;
namespace Ossastra.UserInterface
{
    [DefaultExecutionOrder(250)]
    public sealed class CombatFeedbackView:MonoBehaviour
    {
        private sealed class Stroke {public LineRenderer line;public float age;public Color color;}
        private sealed class Observation
        {
            public Ossastra.Characters.CharacterRoot root;
            public Guid dodge;
            public int hp,bombs,landings,skillLandings;
        }
        [Tooltip("历史场景媒体引用，仅保留序列化兼容；本组件不再播放。正式映射见SfxCatalogSO。")]
        [SerializeField] private AudioClip release,hit,hurt;
        [SerializeField] private HealingBottleService bottle;
        private readonly List<Action> unsubscribe=new List<Action>();
        private readonly List<Stroke> strokes=new List<Stroke>();
        private readonly List<Observation> observations=new List<Observation>();

        private Material material;
        private int bottleUses;
        public int ReleaseCues {get;private set;}
        public int HitCues {get;private set;}
        public int MotionCues {get;private set;}
        public int HealingCues {get;private set;}
        private static readonly Color cyan=new Color(.56f,.85f,.95f),gold=new Color(1,.8f,.35f);
        private void Start()
        {

            material=new Material(Shader.Find("Sprites/Default"));
            foreach(var root in FindObjectsOfType<Ossastra.Characters.CharacterRoot>())
            {
                var owner=root;var abilities=owner.GetComponent<CharacterAbilities>();Action<ActionFact> released=f=>{if(!isActiveAndEnabled||f.Kind!=ActionFactKind.Released||abilities!=null&&abilities.OwnsRelease(f.ActionId))return;ReleaseCues++;Arc(owner.transform.position,owner.DataBus.AttackDirection,gold,.9f);};
                owner.ActionObserved+=released;unsubscribe.Add(()=>{if(owner!=null)owner.ActionObserved-=released;});
                if(abilities!=null)
                {
                    Action<Guid,string,Vector3> feedback=(id,kind,position)=>
                    {
                        if(!isActiveAndEnabled)return;
                        if(kind=="accepted"){Arc(position,Vector3.right,cyan,.2f,true);}
                        else if(kind=="cancel"){Arc(position,Vector3.right,Color.gray,.3f,true);}
                        else if(kind=="end"){Arc(position,Vector3.right,cyan,.12f,true);}
                        else if(kind=="release"){ReleaseCues++;Arc(position,owner.DataBus.AttackDirection,gold,.9f);}
                        else if(kind=="hit"){HitCues++;Arc(position,Vector3.right,gold,.45f,true);}
                        else if(kind=="guard"){HitCues++;Arc(position,owner.DataBus.AttackDirection,cyan,.7f);}
                        else if(kind=="launch"||kind=="land"){MotionCues++;Arc(position,Vector3.right,kind=="launch"?cyan:gold,kind=="launch"?.5f:1.25f,true);}
                        else if(kind=="reload"){MotionCues++;Arc(position,Vector3.right,cyan,.35f,true);}
                    };
                    abilities.Feedback+=feedback;unsubscribe.Add(()=>{if(abilities!=null)abilities.Feedback-=feedback;});
                }
                var guard=owner.GetComponent<ShieldGuard>();if(guard!=null){Action<HitOutcome> blocked=f=>{if(!isActiveAndEnabled)return;HitCues++;Arc(owner.transform.position,owner.DataBus.AttackDirection,cyan,.7f);};guard.Guarded+=blocked;unsubscribe.Add(()=>{if(guard!=null)guard.Guarded-=blocked;});}
                observations.Add(new Observation{root=root,hp=root.Vitals.Health.Current});
            }
            foreach(var melee in FindObjectsOfType<CombatMeleeHit>())
            {
                var source=melee;Action<Guid,bool> receipt=(id,applied)=>{if(!isActiveAndEnabled||!applied)return;HitCues++;Arc(source.transform.position,source.transform.forward,gold,.4f);};
                source.Resolved+=receipt;unsubscribe.Add(()=>{if(source!=null)source.Resolved-=receipt;});
            }
            bottleUses=bottle==null?0:bottle.UseCount;
        }
        private void Arc(Vector3 position,Vector3 direction,Color color,float radius,bool ring=false)
        {
            if(strokes.Count>=32)return;
            var go=new GameObject("ConfirmedCombatFeedback");go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.widthMultiplier=.035f;line.numCapVertices=3;line.positionCount=ring?49:20;
            var angle=Mathf.Atan2(direction.z,direction.x);var begin=ring?0:angle-.8f;var span=ring?Mathf.PI*2:1.6f;
            for(int i=0;i<line.positionCount;i++){var a=begin+span*i/(line.positionCount-1);line.SetPosition(i,position+new Vector3(Mathf.Cos(a)*radius,.15f,Mathf.Sin(a)*radius));}
            strokes.Add(new Stroke{line=line,color=color});
        }
        private void LateUpdate()
        {

            foreach(var o in observations)
            {
                var root=o.root;if(root==null)continue;
                var hp=root.Vitals.Health.Current;if(hp<o.hp){Arc(root.transform.position,Vector3.right,Color.red,.4f,true);}o.hp=hp;
                var dodge=root.GetComponent<CharacterDodge>();if(dodge!=null&&dodge.IsActive&&o.dodge!=dodge.ActionId){o.dodge=dodge.ActionId;MotionCues++;Arc(root.transform.position,Vector3.right,cyan,.55f,true);}
                var offhand=root.GetComponent<CharacterOffhand>();if(offhand!=null&&offhand.ReleasedCount!=o.bombs){o.bombs=offhand.ReleasedCount;}
                if(offhand!=null&&offhand.LandedCount!=o.landings){o.landings=offhand.LandedCount;HitCues++;Arc(offhand.LastLanding,Vector3.right,gold,1.25f,true);}
                var skills=root.GetComponent<CharacterSkills>();if(skills!=null&&skills.ResolvedCount!=o.skillLandings){o.skillLandings=skills.ResolvedCount;HitCues++;Arc(skills.LastEffectPosition,Vector3.right,gold,1.25f,true);}
            }
            if(bottle!=null&&bottle.UseCount!=bottleUses){bottleUses=bottle.UseCount;HealingCues++;foreach(var o in observations)if(o.root!=null&&o.root.IsAlive)Arc(o.root.transform.position,Vector3.right,Color.green,.65f,true);}
            for(int i=strokes.Count-1;i>=0;i--){var stroke=strokes[i];stroke.age+=Time.deltaTime;if(stroke.age>=.25f){Destroy(stroke.line.gameObject);strokes.RemoveAt(i);continue;}var c=stroke.color;c.a=1-stroke.age/.25f;stroke.line.startColor=stroke.line.endColor=c;}
        }
        private void OnDisable(){foreach(var stroke in strokes)if(stroke.line!=null)Destroy(stroke.line.gameObject);strokes.Clear();}
        private void OnDestroy(){foreach(var remove in unsubscribe)remove();foreach(var s in strokes)if(s.line!=null)Destroy(s.line.gameObject);if(material!=null)Destroy(material);}
    }
}
