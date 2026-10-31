/* 脚本：CraftingPresentationContracts
 * 功能：固定工位配方界面的只读行、预览、确认回执与宿主端口。
 * 边界：不拥有库存、装备实例、资格、会话身份或保存；RequestId只由宿主签发。
 */
using System.Collections.Generic;
using UnityEngine;

namespace Ossastra.Crafting.Presentation
{
    public interface ICraftingPresentationHost
    {
        string StationName { get; }
        bool IsSessionValid { get; }
        long RefreshRevision { get; }
        IReadOnlyList<CraftingRecipePresentation> Recipes { get; }
        CraftingPreviewPresentation Preview(string recipeId, int quantity);
        CraftingResultPresentation Confirm(string requestId);
        void CloseSession();
    }

    public sealed class CraftingRecipePresentation
    {
        public string Id { get; }
        public string Name { get; }
        public string Category { get; }
        public Sprite Icon { get; }
        public string Description { get; }

        public CraftingRecipePresentation(string id, string name, string category, Sprite icon, string description)
        { Id=id; Name=name; Category=category; Icon=icon; Description=description; }
    }

    public sealed class CraftingPreviewPresentation
    {
        public bool Ok { get; }
        public string Reason { get; }
        public string RequestId { get; }
        public int Qty { get; }
        public int MaxBatch { get; }
        public string OutputSummary { get; }
        public string MaterialsSummary { get; }

        public CraftingPreviewPresentation(bool ok, string reason, string requestId, int qty, int maxBatch, string outputSummary, string materialsSummary)
        { Ok=ok; Reason=reason; RequestId=requestId; Qty=qty; MaxBatch=maxBatch; OutputSummary=outputSummary; MaterialsSummary=materialsSummary; }
    }

    public sealed class CraftingResultPresentation
    {
        public bool Ok { get; }
        public string Reason { get; }
        public string OutputSummary { get; }

        public CraftingResultPresentation(bool ok, string reason, string outputSummary)
        { Ok=ok; Reason=reason; OutputSummary=outputSummary; }
    }
}
