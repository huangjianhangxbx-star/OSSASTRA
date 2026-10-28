/* 脚本：IInventoryStartupAdapter
 * 功能：由宿主在任何初始授予前选择完整恢复或首次播种，成功后开放唯一库存。
 * 边界：不创建第二份Ledger，不接管角色初始化、输入或音频；恢复失败不能退回播种。
 */
namespace Ossastra.Inventory
{
    public interface IInventoryStartupAdapter
    {
        bool RestoreOrSeed(InventorySession session, out bool seed, out string error);
        void OnInventoryReady(InventorySession session);
    }
}
