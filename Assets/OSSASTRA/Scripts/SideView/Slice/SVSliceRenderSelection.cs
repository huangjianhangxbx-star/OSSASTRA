using Ossastra.WorldRendering.SideView;
using UnityEngine;
using Recipe = Ossastra.WorldRendering.SideView.SideViewRenderComparison.Recipe;
using MaterialMode = Ossastra.WorldRendering.SideView.SideViewRenderComparison.AceCharacterMaterial;

namespace Ossastra.SideView.Slice
{
    [DisallowMultipleComponent, DefaultExecutionOrder(500), RequireComponent(typeof(SideViewRenderComparison))]
    public sealed class SVSliceRenderSelection : MonoBehaviour
    {
        private static Recipe? selectedRecipe;
        private static MaterialMode selectedMaterial = MaterialMode.Fusion;
        private SideViewRenderComparison comparison;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetChoice()
        {
            selectedRecipe = null;
            selectedMaterial = MaterialMode.Fusion;
        }

        private void Start()
        {
            comparison = GetComponent<SideViewRenderComparison>();
            switch (selectedRecipe)
            {
                case Recipe.Legacy: comparison.SelectLegacy(); break;
                case Recipe.Silksong: comparison.SelectSilksong(); break;
                case Recipe.Ace: comparison.SelectAce(); break;
            }
            if (!selectedRecipe.HasValue) return;
            switch (selectedMaterial)
            {
                case MaterialMode.Original: comparison.SelectAceOriginal(); break;
                case MaterialMode.Replica: comparison.SelectAceReplica(); break;
                case MaterialMode.Fusion: comparison.SelectAceFusion(); break;
            }
        }

        private void LateUpdate()
        {
            if (comparison == null || !comparison.Active.HasValue) return;
            selectedRecipe = comparison.Active.Value;
            if (selectedRecipe == Recipe.Ace) selectedMaterial = comparison.ActiveAceMaterial;
        }
    }
}