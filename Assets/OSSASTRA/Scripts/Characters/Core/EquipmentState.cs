/* 脚本：EquipmentState
 * 功能：本体左右手独立实例及左手耐久；切换保留原实例，真实释放/防御按身份去重磨损。
 * 边界：不启动动作、不管理CD或背包；调用方验证空闲后才允许切装。
 */
using System;
using System.Collections.Generic;
namespace Ossastra.CharacterCore
{
    public enum OffhandKind { None, Shield, Bomb }
    public sealed class EquipmentState
    {
        private sealed class Item
        {
            public readonly string id=Guid.NewGuid().ToString("N");
            public int durability;
            public readonly HashSet<Guid> wear=new HashSet<Guid>();
        }
        private readonly Dictionary<OffhandKind,Item> items=new Dictionary<OffhandKind,Item>();
        public string OwnerId {get;}
        public string RightInstanceId {get;}
        public OffhandKind LeftKind {get;private set;}
        public string LeftInstanceId=>LeftKind==OffhandKind.None?"none":OwnerId+":left:"+items[LeftKind].id;
        public int Durability=>LeftKind==OffhandKind.None?0:items[LeftKind].durability;
        public EquipmentState(string actorId,int durability)
        {
            if(string.IsNullOrWhiteSpace(actorId)||durability<=0)throw new ArgumentException("Invalid equipment owner/durability");
            OwnerId=actorId;RightInstanceId=actorId+":right:"+Guid.NewGuid().ToString("N");
            items[OffhandKind.Shield]=new Item{durability=durability};items[OffhandKind.Bomb]=new Item{durability=durability};
        }
        public bool TryEquip(OffhandKind kind,bool idle)
        {if(!idle||!Enum.IsDefined(typeof(OffhandKind),kind))return false;LeftKind=kind;return true;}
        public bool Wear(Guid token)
        {
            if(LeftKind==OffhandKind.None||token==Guid.Empty)return false;
            var item=items[LeftKind];if(item.durability<=0||!item.wear.Add(token))return false;
            item.durability--;return true;
        }
    }
}
