/* 脚本：MapResearchPartyBridge
 * 功能：外层装配唯一组队会话与多个观察消费者，提供原Current的合法跟随Transform。
 * 边界：不缓存第二份Current、不推进输入/角色/动画/战斗；敌人只读观察，不参与玩家捕获。
 * 生命周期：显式owner，批量绑定失败精确回滚；释放只触及本桥持有的端口和捕获。
 */
using System;
using System.Collections.Generic;
using Ossastra.Characters;
using Ossastra.Characters.ViewSpace;
using Ossastra.Enemies;
using Ossastra.MapResearch;
using UnityEngine;
using MapCharacterRoot=Ossastra.Characters.CharacterRoot;
using MapEnemyRoot=Ossastra.Enemies.EnemyRoot;
namespace Ossastra.MapResearchIntegration
{
    [DisallowMultipleComponent]
    public sealed class MapResearchPartyBridge:MonoBehaviour,IMapResearchSession
    {
        public CharacterControlCoordinator coordinator;
        public MapCharacterRoot[] characters=Array.Empty<MapCharacterRoot>();
        public MapEnemyRoot[] enemies=Array.Empty<MapEnemyRoot>();
        private readonly List<ICharacterViewConsumer> owned=new List<ICharacterViewConsumer>();
        private object contextOwner;
        private bool captured;
        public Transform FollowTarget
        {
            get
            {
                var current=coordinator==null?null:coordinator.Current;
                return current!=null&&current.IsAlive&&current.isActiveAndEnabled?current.transform:null;
            }
        }
        public string ViewDiagnostics=>"主控："+(coordinator==null||coordinator.Current==null?"无合法主控":coordinator.Current.ActorId)+
            "  观察注册 "+RegisteredCount+"  控制代际 "+(coordinator==null?0:coordinator.ControlRevision)+"；观察模式不暂停战斗";
        public int RegisteredCount{get{int count=0;foreach(var consumer in owned)if(Alive(consumer))count++;return count;}}
        public bool BindViewContext(object owner,ICharacterViewContext context)
        {
            if(!isActiveAndEnabled||owner==null||context==null||contextOwner!=null)return false;
            if(characters==null||characters.Length==0)return false;
            var candidates=new List<ICharacterViewConsumer>();
            foreach(var character in characters)
            {
                if(character==null)return false;
                if(candidates.Contains(character))return false;
                candidates.Add(character);
            }
            if(enemies!=null)foreach(var enemy in enemies)
            {
                if(enemy==null)return false;
                var adapter=enemy.GetComponent<EnemyViewSpaceAdapter>();
                if(adapter==null||candidates.Contains(adapter))return false;
                candidates.Add(adapter);
            }
            foreach(var consumer in candidates)
            {
                if(!consumer.BindViewContext(owner,context))
                {
                    foreach(var previous in owned)if(Alive(previous))previous.ReleaseViewContext(owner);
                    owned.Clear();return false;
                }
                owned.Add(consumer);
            }
            contextOwner=owner;return true;
        }
        public bool SetViewInputCapture(object owner,bool value)
        {
            if(contextOwner==null||!ReferenceEquals(contextOwner,owner))return false;
            if(captured==value)return true;
            if(!value)
            {
                // 解锁不能因某个观察组件已移除而事务回滚，否则退出结构模式后整队仍被捕获。
                foreach(var character in characters)if(character!=null&&!character.SetViewInputCapture(owner,false))
                {character.ReleaseViewContext(owner);owned.Remove(character);}
                if(coordinator!=null)coordinator.SetInputCapture(owner,false);
                captured=false;return true;
            }
            var changed=new List<MapCharacterRoot>();
            foreach(var character in characters)
            {
                if(character==null)continue;
                if(!character.SetViewInputCapture(owner,value))
                {
                    foreach(var previous in changed)previous.SetViewInputCapture(owner,!value);
                    return false;
                }
                changed.Add(character);
            }
            if(coordinator!=null)coordinator.SetInputCapture(owner,value);
            captured=value;return true;
        }
        public void ReleaseViewContext(object owner)
        {
            if(contextOwner==null||!ReferenceEquals(contextOwner,owner))return;
            // 即使一个实体已移除也必须释放组内捕获，不触及其它owner。
            if(coordinator!=null)coordinator.SetInputCapture(owner,false);
            if(characters!=null)foreach(var character in characters)if(character!=null)character.SetViewInputCapture(owner,false);
            foreach(var consumer in owned)if(Alive(consumer))consumer.ReleaseViewContext(owner);
            owned.Clear();contextOwner=null;captured=false;
        }
        private static bool Alive(ICharacterViewConsumer consumer)=>consumer!=null&&(!(consumer is UnityEngine.Object obj)||obj!=null);
        private void OnDisable(){if(contextOwner!=null)ReleaseViewContext(contextOwner);}
        private void OnDestroy(){if(contextOwner!=null)ReleaseViewContext(contextOwner);}
    }
}
