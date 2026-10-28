/* 脚本：InventoryAssemblyAccess
 * 功能：只向纯交易协调程序集开放候选库存共同提交能力；Runtime/UI不能提交一半事务。
 */
using System.Runtime.CompilerServices;
[assembly:InternalsVisibleTo("Ossastra.Commerce.Core")]
