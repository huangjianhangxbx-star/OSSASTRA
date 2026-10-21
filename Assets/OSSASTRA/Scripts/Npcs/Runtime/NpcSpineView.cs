/* NPC独立二维适配：根在LateUpdate提交Idle。无输入、Health、EnemyBrain或动作时钟。 */
using System;
using Ossastra.AnimationCore;
using Ossastra.AnimationPresentation;
using UnityEngine;
namespace Ossastra.Npcs
{
    [DisallowMultipleComponent]
    public sealed class NpcSpineView:MonoBehaviour,IEntityPresentation
    {
        [SerializeField] private EntitySpinePresenter presenter;
        private NpcRoot root;
        private void Awake(){root=GetComponent<NpcRoot>();}
        public void Present(){if(isActiveAndEnabled&&root!=null&&presenter!=null)presenter.Present(new Ossastra.AnimationCore.AnimationIntent(root.NpcId,AnimationSemantic.Idle,"npc-idle",Guid.Empty,0),1,false);}
    }
}
