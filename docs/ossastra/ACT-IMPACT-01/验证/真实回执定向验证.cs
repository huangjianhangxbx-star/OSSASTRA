/* 团结后台 execute_code 入口。预览场景内冻结真实 SO，投递真实 AttackRelease，验证动作终结、死亡与同一实体复活后的效果。
 * 读取运动系统已经接受的曲线总请求。碰撞实移单独由运动定向验证测量。
 */
using System;
using System.Reflection;
using Ossastra.CharacterCore;
using Ossastra.Characters;
using Ossastra.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using EnemyActor = Ossastra.Enemies.EnemyRoot;

public static class ImpactContactProbe
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    static CombatVitals Body(Scene scene, string name, CombatFaction faction, float x)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = Vector3.right * x;
        go.AddComponent<CharacterController>();
        var actor = go.AddComponent<EnemyActor>();
        go.AddComponent<Ossastra.Enemies.EnemyMovement>();
        var vitals = go.GetComponent<CombatVitals>();
        typeof(CombatVitals).GetField("faction", Private).SetValue(vitals,faction);
        typeof(EnemyActor).GetField("vitals", Private).SetValue(actor,vitals);
        typeof(CombatVitals).GetProperty("SpatialPlane", Private).SetValue(vitals, CombatPlane.SideXY);
        Check(vitals.Initialize(name), "探针身份无法初始化");
        return vitals;
    }
    static float Consume(CombatVitals target)
    {
        var motor = target.GetComponent<ActionImpactMotor>();
        if (!motor) return 0;
        return ((Vector2)typeof(ActionImpactMotor).GetMethod("Step", Private).Invoke(motor, new object[]{1f,null})).x;
    }
    public static string Run()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        var definition = ScriptableObject.CreateInstance<ActionDefinitionSO>();
        try
        {
            var attacker = Body(preview,"impact-source",CombatFaction.Enemy,0);
            var target = Body(preview,"impact-target",CombatFaction.Allied,3);
            var another = Body(preview,"impact-other",CombatFaction.Allied,4);
            var id = Guid.NewGuid();
            var runType = typeof(EnemyActionImpactBinding).Assembly.GetType("Ossastra.Characters.ActionImpactRun",true);
            var accept = runType.GetMethod("AcceptEnemy",BindingFlags.Static|BindingFlags.NonPublic);
            var actor = attacker.GetComponent<EnemyActor>();
            object Start() => accept.Invoke(null,new object[]{actor,definition,id});
            Check(Start()==null,"空列表应旁路");
            var push = new TargetKnockbackEffect { distance=.6f, channel="melee" };
            var recoil = new SelfRecoilEffect { distance=.12f };
            definition.effects.entries.Add(push); definition.effects.entries.Add(recoil);
            definition.effects.bypass = true;
            Check(Start()==null,"旁路未生效");
            definition.effects.bypass = false;
            var run = Start(); Check(run!=null,"接收快照为空");
            push.distance=2.4f; recoil.enabled=false; // 下一次出招才可看见
            var bind=runType.GetMethod("Bind",Private);
            var first=AttackRelease.ForContactWindow(id,0,attacker.Health.Id,5,0);
            var delayed=AttackRelease.ForEmission(id,1,attacker.Health.Id,5,0);
            var oldLifeRelease = AttackRelease.ForEmission(id,2,attacker.Health.Id,5,0);
            bind.Invoke(run,new object[]{first,"melee"}); bind.Invoke(run,new object[]{delayed,"melee"});
            bind.Invoke(run,new object[]{oldLifeRelease,"melee"});
            var hit=CombatContactGateway.Resolve(first,attacker,new IHitTarget[]{target,target,another},attacker.transform.position);
            Check(hit.Count==2 && target.Health.Current==target.Health.Maximum-5 && another.Health.Current==another.Health.Maximum-5,"真实双靶结算或 Collider 去重");
            float firstTarget=Consume(target),otherTarget=Consume(another),self=Consume(attacker);
            Check(Math.Abs(firstTarget-.6f)<.001f && Math.Abs(otherTarget-.6f)<.001f && Math.Abs(self+.12f)<.001f,"冻结/双目标/反冲："+firstTarget+","+otherTarget+","+self);
            var repeated=CombatContactGateway.Resolve(first,attacker,new[]{(IHitTarget)target},attacker.transform.position);
            Check(repeated.Count==1 && repeated[0].Reason==HitReason.Duplicate && Consume(target)==0 && Consume(attacker)==0,"窗口重复回执触发运动");
            runType.GetMethod("End",Private).Invoke(run,null);
            var detached=CombatContactGateway.Resolve(delayed,attacker,new[]{(IHitTarget)target},attacker.transform.position);
            float targetAfter=Consume(target),selfAfter=Consume(attacker);
            Check(detached.Count==1 && detached[0].Reason==HitReason.Applied && Math.Abs(targetAfter-.6f)<.001f && selfAfter==0,"离手结算/旧窗口污染");
            int oldDeaths = attacker.DeathCount;
            var fatal = AttackRelease.ForEmission(Guid.NewGuid(),0,another.Health.Id,attacker.Health.Maximum,0);
            var death = CombatContactGateway.Resolve(fatal, another,new[]{(IHitTarget)attacker},another.transform.position);
            Check(death.Count==1 && !attacker.IsAlive && attacker.DeathCount==oldDeaths+1,"未触发真实死亡");
            attacker.RestoreForDevelopment();
            Check(attacker.IsAlive && attacker.DeathCount==oldDeaths+1,"原敌人生命未恢复");
            var late = CombatContactGateway.Resolve(oldLifeRelease,attacker,new[]{(IHitTarget)target},attacker.transform.position);
            float stale = Consume(target);
            Check(late.Count==1 && late[0].Reason==HitReason.Applied && stale==0 && Consume(attacker)==0,
                "敌人复活后，旧生命的在途回执产生了运动："+stale);
            id = Guid.NewGuid();
            var newLife = Start();
            var fresh = AttackRelease.ForEmission(id,0,attacker.Health.Id,5,0);
            bind.Invoke(newLife,new object[]{fresh,"melee"});
            var freshHit = CombatContactGateway.Resolve(fresh,attacker,new[]{(IHitTarget)target},attacker.transform.position);
            float freshDistance = Consume(target);
            Check(freshHit.Count==1 && freshHit[0].Reason==HitReason.Applied && Math.Abs(freshDistance-2.4f)<.001f,
                "同一敌人复活后新动作未使用新快照："+freshDistance);
            return $"旧生命迟到推移 {stale:F3}，复活后新招式 {freshDistance:F3}。真实接触/快照通过。第一靶 {firstTarget:F3}，第二靶 {otherTarget:F3}，源反冲 {self:F3}。终结后同一在途释放，目标 {targetAfter:F3}，源 {selfAfter:F3}。";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
