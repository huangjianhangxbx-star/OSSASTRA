using UnityEngine;

namespace Ossastra.SideView.Slice
{
    [DisallowMultipleComponent]
    public sealed class SVSliceEnemySpawn : MonoBehaviour
    {
        public string stableId;
        public string candidateId;
        public bool isBoss;
    }
}