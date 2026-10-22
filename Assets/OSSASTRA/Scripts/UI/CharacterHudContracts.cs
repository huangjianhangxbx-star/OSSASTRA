/* 脚本：CharacterHudContracts
 * 功能：HUD读取快照、提交命令的边界；视图不持有角色业务组件。
 * 边界：快照只用于本帧呈现，不成为第二份HP、装备或冷却权威。
 */
using UnityEngine;
namespace Ossastra.UserInterface
{
    public struct CharacterHudSnapshot
    {
        public string actorId,name,rightHand,leftHand,condition,rightInstanceId,leftInstanceId,leftDefinitionId;
        public bool alive,controlled;
        public float hp,maxHp,grayHp,stamina,maxStamina,posture,maxPosture;
        public int durability;
        public string[] skillNames,skillStates;
        public float[] skillCooldowns;
    }
    public interface ICharacterHudPort
    {
        CharacterHudSnapshot Read(bool companion);
        CharacterHudSnapshot ReadCurrent();
        int BottleCount {get;}
        string Notice {get;}
        void SetModalCapture(object owner,bool captured);
        void UseBottle();
        void UseSkill(int slot);
        void SwitchCharacter();
        void Equip(string actorId,string definitionId);
        (string id,string name,Sprite sprite)[] EquipmentChoices(string actorId);
        (string id,string name)[] SkillChoices(string actorId);
        void ConfigureSkill(string actorId,int slot,string definitionId);
    }
}
