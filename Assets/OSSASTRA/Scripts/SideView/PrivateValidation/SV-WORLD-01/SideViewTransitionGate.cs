/* 脚本：SideViewTransitionGate
 * 功能：实体入口只提交换区请求；库存与共同存档就绪时才允许旅行。
 * 边界：不直接改世界状态，不卸载 Scene。
 */
using UnityEngine;
using Ossastra.Characters;
using Ossastra.Inventory;
using Ossastra.Commerce.Runtime;

namespace Ossastra.SideViewWorld
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SideViewTransitionGate : MonoBehaviour
    {
        public string gateId;
        public string destinationScenePath;
        public string destinationGateId;
        public CharacterControlCoordinator party;
        public InventorySession inventory;
        public CommerceService commerce;

        private bool armed = true;
        public bool Armed => armed;

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            SideViewTransitionRuntime.Ensure();
        }

        private void OnTriggerEnter(Collider other) => TryPhysicalEntry(other);
        private void OnTriggerStay(Collider other) => TryPhysicalEntry(other);

        private void TryPhysicalEntry(Collider other)
        {
            if (!armed || other == null || party == null) return;
            var actor = other.GetComponentInParent<Ossastra.Characters.CharacterRoot>();
            if (actor == null || actor != party.Current) return;
            if (TryEnter()) armed = false;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || party == null) return;
            var actor = other.GetComponentInParent<Ossastra.Characters.CharacterRoot>();
            if (actor != null && actor == party.Current) armed = true;
        }

        /// <summary>Debug panel and physical trigger use the same transition path.</summary>
        public bool TryEnter() => SideViewTransitionRuntime.Ensure().TryBegin(this);

        private void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
