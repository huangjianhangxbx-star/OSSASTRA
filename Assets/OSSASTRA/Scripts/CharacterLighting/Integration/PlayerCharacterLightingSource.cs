/* 功能：把既有玩家根与装备表现适配为只读光感来源；由玩家根组装一次。
 * 边界：不拥有角色生命/动作/显隐，只读取原本体和当前装备，不另建更新循环。
 */
using System.Collections.Generic;
using UnityEngine;
using Ossastra.Characters;
namespace Ossastra.CharacterLighting
{
 public sealed class PlayerCharacterLightingSource:ICharacterLightingSource
 {
  readonly Ossastra.Characters.CharacterRoot root;
  readonly CharacterEquipmentSpineView spine;
  readonly CharacterEquipmentView sprite;
  public PlayerCharacterLightingSource(Ossastra.Characters.CharacterRoot root){this.root=root;if(root!=null){spine=root.GetComponent<CharacterEquipmentSpineView>();sprite=root.GetComponent<CharacterEquipmentView>();}}
  public Component Owner=>root;
  public string EntityId=>root==null?"":root.ActorId;
  public bool IsAvailable=>root!=null&&root.isActiveAndEnabled&&root.IsAlive;
  public void AppendEquipmentRenderers(List<Renderer> target)
  {
   if(spine!=null){if(spine.LeftSkeleton!=null)target.Add(spine.LeftSkeleton.GetComponent<Renderer>());if(spine.RightSkeleton!=null)target.Add(spine.RightSkeleton.GetComponent<Renderer>());}
   if(sprite!=null)target.Add(sprite.PresentedRenderer);
  }
 }
}
