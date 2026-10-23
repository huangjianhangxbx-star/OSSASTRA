/* 脚本：InventoryUseContracts
 * 功能：库存详情向真实消费者提交使用请求的窄接口。
 * 边界：UI不直接扣数量或治疗；消费者必须经同一来源事务与能力服务。
 */
namespace Ossastra.Inventory
{
    public interface IInventoryItemUse
    {
        bool TryUse(string itemId);
        string LastResult {get;}
    }
}
