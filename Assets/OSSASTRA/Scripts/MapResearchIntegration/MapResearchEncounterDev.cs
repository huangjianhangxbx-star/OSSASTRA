/* 脚本：MapResearchEncounterDev
 * 功能：样地显式遭遇复位与状态提示；仅调用原角色/敌人恢复入口，保留镜头yaw与缩放。
 * 边界：无自动复活、HP直写、TimeScale或正式UI替代；只作用本场显式实体。
 */
using System;
using Ossastra.Characters;
using Ossastra.Enemies;
using UnityEngine;
using UnityEngine.InputSystem;
using MapCharacterRoot=Ossastra.Characters.CharacterRoot;
using MapEnemyRoot=Ossastra.Enemies.EnemyRoot;
namespace Ossastra.MapResearchIntegration
{
    public sealed class MapResearchEncounterDev:MonoBehaviour
    {
        public CharacterControlCoordinator coordinator;
        public MapCharacterRoot[] characters=Array.Empty<MapCharacterRoot>();
        public MapEnemyRoot[] enemies=Array.Empty<MapEnemyRoot>();
        private Vector3[] characterPositions,enemyPositions;
        private void Awake()
        {
            characterPositions=new Vector3[characters.Length];enemyPositions=new Vector3[enemies.Length];
            for(int i=0;i<characters.Length;i++)if(characters[i]!=null)characterPositions[i]=characters[i].transform.position;
            for(int i=0;i<enemies.Length;i++)if(enemies[i]!=null)enemyPositions[i]=enemies[i].transform.position;
        }
        public void ResetEncounter()
        {
            if(characterPositions==null)return;
            for(int i=0;i<characters.Length;i++)
            {
                var character=characters[i];if(character==null)continue;
                var controller=character.GetComponent<CharacterController>();bool wasEnabled=controller!=null&&controller.enabled;
                if(wasEnabled)controller.enabled=false;character.transform.position=characterPositions[i];if(wasEnabled)controller.enabled=true;
                character.CancelAction("research-encounter-reset");
                if(character.Vitals!=null)character.Vitals.RestoreForDevelopment();
                var stamina=character.GetComponent<CharacterStamina>();if(stamina!=null)stamina.Restore();
            }
            for(int i=0;i<enemies.Length;i++)
            {
                var enemy=enemies[i];if(enemy==null)continue;
                var controller=enemy.GetComponent<CharacterController>();bool wasEnabled=controller!=null&&controller.enabled;
                if(wasEnabled)controller.enabled=false;enemy.transform.position=enemyPositions[i];if(wasEnabled)controller.enabled=true;
                enemy.RestoreForDevelopment();
            }
            if(coordinator!=null)coordinator.RestoreControlForDevelopment();
        }
        private void OnGUI()
        {
            if(!Application.isPlaying)return;
            var area=new Rect(12,Screen.height-90,690,78);GUI.Box(area,GUIContent.none);
            string binding=coordinator==null||coordinator.switchAction==null?"未配置":coordinator.switchAction.GetBindingDisplayString();
            GUI.Label(new Rect(24,area.y+5,650,22),"双角色单敌人验证；切人 "+binding+"｜观察模式不暂停战斗");
            if(GUI.Button(new Rect(24,area.y+32,160,28),"重置本次遭遇"))ResetEncounter();
            string state="";foreach(var c in characters)if(c!=null)state+=c.ActorId+" HP "+(c.Vitals==null||c.Vitals.Health==null?"?":c.Vitals.Health.Current.ToString())+"  ";
            foreach(var e in enemies)if(e!=null)state+=e.ActorId+" HP "+(e.Vitals==null||e.Vitals.Health==null?"?":e.Vitals.Health.Current.ToString());
            GUI.Label(new Rect(195,area.y+37,480,25),state);
        }
        private void Update()
        {
            foreach(var character in characters)if(character!=null)
            {
                var input=character.GetComponent<CharacterInputHandler>();
                if(input!=null)input.RegisterPointerRegion(this,new Rect(12,Screen.height-90,690,78));
            }
        }
        private void OnDisable()
        {
            foreach(var character in characters)if(character!=null)
            {var input=character.GetComponent<CharacterInputHandler>();if(input!=null)input.RemovePointerRegion(this);}
        }
    }
}
