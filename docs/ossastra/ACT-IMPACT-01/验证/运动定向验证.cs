// 团结后台 execute_code 的独立模块验证。临时预览场景创建在编辑器内，释放后不写工程资产。
// 以生产运动接收器与 EnemyMovement.Tick 的真实胶囊提交测量位移、抛物及碰顶。无正式 AI/输入模拟。
using System;
using System.Reflection;
using System.Text;
using Ossastra.Combat;
using Ossastra.Characters;
using Ossastra.Enemies;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class ImpactMovementProbe
{
    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
    public static string Run()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var profile = ScriptableObject.CreateInstance<CharacterJumpProfile>();
        try
        {
            var actor = new GameObject("Motion probe");
            SceneManager.MoveGameObjectToScene(actor, scene);
            var body = actor.AddComponent<CharacterController>();
            body.radius = .2f; body.height = 1; body.center = Vector3.up * .5f;
            body.skinWidth = .01f; body.minMoveDistance = 0; body.stepOffset = .1f;
            var player = actor.AddComponent<CharacterMovement>();
            player.ConfigureSideView(profile);
            var vitals = actor.AddComponent<CombatVitals>();
            Check(vitals.Initialize("impact-probe"), "vitals");
            var mover = actor.AddComponent<EnemyMovement>();
            typeof(EnemyMovement).GetField("body", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mover, body);
            var motor = actor.AddComponent<ActionImpactMotor>();
            // Edit Mode 的普通 Mono 不运行 OnEnable/Awake；只绑定生产中 Awake/OnEnable 会写入的引用。
            typeof(EnemyMovement).GetProperty("Impact", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mover, motor);
            var self = Guid.NewGuid(); var victim = Guid.NewGuid(); string why;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(25,1,5);
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(ceiling, scene);
            ceiling.transform.position = Vector3.up * 1.6f;
            ceiling.transform.localScale = new Vector3(5,.1f,5);
            Physics.SyncTransforms();
            var curve = AnimationCurve.EaseInOut(0,0,1,1);
            var log = new StringBuilder();
            foreach (var hz in new[] {30,60,120})
            {
                actor.transform.position = Vector3.up*.02f; Physics.SyncTransforms();
                motor.ResetState();
                Check(ActionImpactMotor.TryDisplace(player,self,Vector2.right * .75f,.17f,curve,false,false,out why),why);
                for(int i=0;i<hz/2;i++) mover.Tick(Vector3.zero,1f/hz);
                Check(Mathf.Abs(actor.transform.position.x-.75f)<.02f,"distance "+hz+" "+actor.transform.position);
                log.Append(hz+"Hz="+actor.transform.position.x.ToString("F4")+"; ");
            }
            motor.ResetState();
            Check(ActionImpactMotor.TryDisplace(player,self,Vector2.left,.25f,curve,false,false,out why),why);
            Check(!ActionImpactMotor.TryDisplace(player,self,Vector2.right*.1f,.25f,curve,false,false,out why),"weak replaced strong");
            motor.PushResistance=.5f;
            Check(ActionImpactMotor.TryDisplace(player,victim,Vector2.right,.25f,curve,true,false,out why),why);
            ActionImpactMotor.Release(player,self);
            Vector3 start=actor.transform.position;
            mover.Tick(Vector3.zero,0f);
            Check(actor.transform.position==start,"zero dt advanced");
            for(int i=0;i<30;i++)mover.Tick(Vector3.zero,1f/60);
            Check(Mathf.Abs(actor.transform.position.x-start.x-.5f)<.02f,"cancel also cleared target reaction");
            start=actor.transform.position;
            Check(ActionImpactMotor.TryDisplace(player,self,Vector2.right*.24f,.16f,curve,false,false,out why),why);
            // 重斩主动前冲与同一释放的命中反冲独立，回执去重由正式投递层决定。
            mover.Tick(Vector3.zero,1f/60);
            Check(ActionImpactMotor.TryDisplace(player,self,Vector2.left*.12f,.14f,curve,false,false,out why,true),why);
            for(int i=0;i<30;i++)mover.Tick(Vector3.zero,1f/60);
            float net=actor.transform.position.x-start.x;
            Check(Mathf.Abs(net-.12f)<.02f,"lunge and recoil net "+net);
            log.Append("lunge+recoil="+net.ToString("F3")+"; ");
            motor.PushResistance=1;
            Check(!ActionImpactMotor.TryLaunch(player,victim,new Vector2(0,9),true,out why),"immune launch");
            motor.PushResistance=0;
            Check(ActionImpactMotor.TryLaunch(player,victim,new Vector2(0,9),true,out why),why);
            float top=0;
            bool above=false;
            for(int i=0;i<120;i++)
            {
                mover.Tick(Vector3.zero,1f/60);
                top=Mathf.Max(top,actor.transform.position.y);
                above|=(body.collisionFlags&CollisionFlags.Above)!=0;
            }
            Check(above && top>.1f && top<.62f && body.isGrounded && !motor.HasMotion,"ceiling/land "+top+" grounded="+body.isGrounded);
            return log+"cancel, stronger displacement, resistance, zero dt, ceiling and landing PASS; peak="+top.ToString("F3");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(profile);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
