/* 脚本：CombatFeedbackView
 * 功能：组织当前场景的战斗反馈。横版使用正式命中批次及可配置效果池，旧场保留既有短时反馈。
 * 边界：不读攻击按键、不接受动作、不执行命中；拒绝请求没有释放反馈。
 * 协作：根动作、能力释放及命中网关为事实源；相机接收冲击请求，声音仍由原适配器消费。
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
        [SerializeField] private bool sideView;
        [SerializeField, Tooltip("横版相机的撞击请求接收者。")] private MonoBehaviour shakeSink;
        [SerializeField] private SideViewCombatEffectSettings sideViewEffects = new SideViewCombatEffectSettings();
        private readonly List<Action> unsubscribe=new List<Action>();
        private readonly List<Stroke> strokes=new List<Stroke>();
        private readonly List<Observation> observations=new List<Observation>();

        private Material material;
        private SideViewCombatEffects effects;
        private bool started;
        private int bottleUses;
        public int ReleaseCues {get;private set;}
        public int HitCues {get;private set;}
        public int MotionCues {get;private set;}
        public int HealingCues {get;private set;}
        public int ActiveSideViewEffects => effects?.ActiveCount ?? 0;
        public int ShakeRequests { get; private set; }
        public Vector3 LastContactPosition { get; private set; }
        public Vector3 LastContactDirection { get; private set; }
        public CombatContactPointQuality LastContactQuality { get; private set; }
        public HitReason? LastContactReason { get; private set; }
        private static readonly Color cyan=new Color(.56f,.85f,.95f),gold=new Color(1,.8f,.35f);

        /// <summary>由横版场装配指定工作模式及相机接收者。原场序列化缺省仍为旧表现。</summary>
        public void ConfigureSideView(MonoBehaviour cameraSink)
        {
            if (cameraSink != null && !(cameraSink is ICombatImpactShakeSink))
                throw new ArgumentException("相机组件不实现战斗冲击接收接口", nameof(cameraSink));
            bool reconnect = started && isActiveAndEnabled;
            if (reconnect) Disconnect();
            sideView = true;
            shakeSink = cameraSink;
            if (reconnect) Connect();
        }

        private void Start()
        {
            started = true;
            Connect();
        }

        private void OnEnable()
        {
            if (started) Connect();
        }

        private void Connect()
        {
            if (sideView)
            {
                ConnectSideView();
                return;
            }
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

        private void ConnectSideView()
        {
            effects = new SideViewCombatEffects(transform, sideViewEffects);
            CombatContactGateway.Resolved += OnSideViewContact;
            unsubscribe.Add(() => CombatContactGateway.Resolved -= OnSideViewContact);
            foreach (var root in FindObjectsOfType<Ossastra.Characters.CharacterRoot>())
            {
                if (root.gameObject.scene != gameObject.scene) continue;
                var owner = root;
                var abilities = owner.GetComponent<CharacterAbilities>();
                Action<ActionFact> released = fact =>
                {
                    if (fact.Kind != ActionFactKind.Released || fact.Release == null ||
                        abilities != null && abilities.OwnsRelease(fact.ActionId)) return;
                    var direction = owner.DataBus.AttackDirection;
                    PlayRelease(owner.transform.position, direction);
                };
                owner.ActionObserved += released;
                unsubscribe.Add(() => { if (owner != null) owner.ActionObserved -= released; });
                if (abilities == null) continue;
                Action<AbilityFeedbackFact> sectorRelease = fact =>
                {
                    if (fact.Kind == "release" && fact.EmissionIndex >= 0 && fact.DeliveryMode == nameof(DeliveryMode.Sector) &&
                        fact.ReleaseDirection.HasValue)
                        PlayRelease(fact.Position, fact.ReleaseDirection.Value);
                };
                abilities.FeedbackObserved += sectorRelease;
                unsubscribe.Add(() => { if (abilities != null) abilities.FeedbackObserved -= sectorRelease; });
            }
        }

        private void PlayRelease(Vector3 position, Vector3 direction)
        {
            if (effects == null) return;
            var recipe = sideViewEffects.release;
            var geometry = new CombatContactGeometry(CombatPlane.SideXY, position, direction, direction);
            var forward = EffectDirection(recipe, geometry, default, direction, false);
            ReleaseCues++;
            effects.Play(SideViewEffectShape.Swing, recipe, position + Vector3.up * .85f, forward);
        }

        private void OnSideViewContact(CombatContactBatch batch)
        {
            if (effects == null || batch.SourceSceneHandle != gameObject.scene.handle) return;
            var geometry = batch.Geometry ?? new CombatContactGeometry(CombatPlane.SideXY, batch.Origin, Vector3.zero, Vector3.zero);
            float strength = 0;
            Vector3 impulse = Vector3.zero;
            foreach (var receipt in batch.Receipts)
            {
                var reason = receipt.Outcome.Reason;
                if (reason == HitReason.Applied && receipt.Outcome.HpAfter < receipt.Outcome.HpBefore)
                    Impact(SideViewEffectShape.Hit, sideViewEffects.hit, receipt.Contact, reason);
                else if (reason == HitReason.Guarded)
                    Impact(SideViewEffectShape.Guard, sideViewEffects.guard, receipt.Contact, reason);
                else if (reason == HitReason.Evaded)
                    Impact(SideViewEffectShape.Evade, sideViewEffects.evade, receipt.Contact, reason);
            }
            if (batch.EnvironmentContact.HasValue)
                Impact(SideViewEffectShape.Surface, sideViewEffects.surface, batch.EnvironmentContact.Value, null);

            // 批内使用最强一次；相机用发射身份合并持续窗口在后续批次的合法接触。
            if (strength > 0 && shakeSink != null && shakeSink is ICombatImpactShakeSink sink)
            {
                sink.RequestImpact(new CombatImpactShakeRequest(batch.ParentExecutionId, batch.ActionId,
                    batch.EmissionIndex, batch.ActorId, impulse, strength));
                ShakeRequests++;
            }

            void Impact(SideViewEffectShape shape, SideViewEffectRecipe recipe, CombatContactPoint contact, HitReason? reason)
            {
                if (recipe == null) return;
                var direction = EffectDirection(recipe, geometry, contact, contact.Position - batch.Origin, true);
                Color? tint = shape == SideViewEffectShape.Hit &&
                    (batch.EffectSemantic == "ice.pulse" || batch.EffectSemantic == "ice.shatter")
                    ? new Color(.54f, .86f, 1f, recipe.tint.a) : (Color?)null;
                effects.Play(shape, recipe, contact.Position, direction, tint);
                HitCues++;
                LastContactPosition = contact.Position;
                LastContactDirection = direction;
                LastContactQuality = contact.Quality;
                LastContactReason = reason;
                float weight = batch.Cause == CombatContactCause.Periodic ? .25f : 1f;
                float value = Mathf.Clamp01(recipe.shake) * weight;
                if (value > strength)
                {
                    strength = value;
                    impulse = direction;
                }
            }
        }

        private static Vector3 EffectDirection(SideViewEffectRecipe recipe, CombatContactGeometry geometry,
            CombatContactPoint contact, Vector3 fallback, bool reverseSurfaceFallback)
        {
            if (recipe == null || recipe.direction == CombatEffectDirection.Fixed) return Vector3.right;
            var travel = geometry.ArrivalDirection.sqrMagnitude > .0001f
                ? geometry.ArrivalDirection : geometry.ReleaseDirection;
            if (travel.sqrMagnitude <= .0001f) travel = fallback;
            Vector3 direction;
            if (recipe.direction == CombatEffectDirection.Surface)
            {
                direction = CombatContactGeometry.Project(contact.Normal ?? Vector3.zero, CombatPlane.SideXY);
                if (direction.sqrMagnitude <= .0001f) direction = reverseSurfaceFallback ? -travel : travel;
            }
            else if (recipe.direction == CombatEffectDirection.Released && geometry.ReleaseDirection.sqrMagnitude > .0001f)
                direction = geometry.ReleaseDirection;
            else direction = travel;
            direction = CombatContactGeometry.Project(direction, CombatPlane.SideXY);
            return direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.right;
        }
        private void Arc(Vector3 position,Vector3 direction,Color color,float radius,bool ring=false)
        {
            if(strokes.Count>=32)return;
            var go=new GameObject("ConfirmedCombatFeedback");go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.widthMultiplier=.035f;line.numCapVertices=3;line.positionCount=ring?49:20;
            var angle=Mathf.Atan2(direction.z,direction.x);var begin=ring?0:angle-.8f;var span=ring?Mathf.PI*2:1.6f;
            for(int i=0;i<line.positionCount;i++){var a=begin+span*i/(line.positionCount-1);line.SetPosition(i,position+new Vector3(Mathf.Cos(a)*radius,.15f,Mathf.Sin(a)*radius));}
            Ossastra.SceneVisibility.Rooms.RoomPresentationBinding.Attach(line,position,true);
            strokes.Add(new Stroke{line=line,color=color});
        }
        private void LateUpdate()
        {
            if (sideView)
            {
                effects?.Tick(Time.deltaTime);
                return;
            }
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
        private void OnDisable() => Disconnect();

        private void Disconnect()
        {
            foreach (var remove in unsubscribe) remove();
            unsubscribe.Clear();
            observations.Clear();
            foreach (var stroke in strokes)
                if (stroke.line != null) Destroy(stroke.line.gameObject);
            strokes.Clear();
            if (material != null) Destroy(material);
            material = null;
            effects?.Dispose();
            effects = null;
        }
    }
}
