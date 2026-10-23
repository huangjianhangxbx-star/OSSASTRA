using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Optional bindings for existing HUD/world plates. The stable hierarchy and
// inactive item template are authored in a prefab; only variable rows are pooled.
public sealed class BattleParticipantStatusWidgets : MonoBehaviour
{
    [SerializeField] private TMP_Text postureText;
    [SerializeField] private UnityEngine.UI.Image postureFill;
    [SerializeField] private Transform statusRoot;
    [SerializeField] private BattleStatusInstanceView statusTemplate;
    private readonly List<BattleStatusInstanceView> items = new List<BattleStatusInstanceView>();

    internal void Apply(BattleProjectedParticipant participant)
    {
        if (!participant.HasSettlementState) return;
        if (postureText != null)
            postureText.text = "架势 " + Mathf.CeilToInt(participant.Posture) + " / " + Mathf.CeilToInt(participant.MaxPosture);
        if (postureFill != null)
            postureFill.fillAmount = participant.MaxPosture > 0f ? participant.Posture / participant.MaxPosture : 0f;
        if (statusRoot == null || statusTemplate == null) return;
        if (statusTemplate.gameObject.activeSelf)
            throw new System.InvalidOperationException("Status item templates must be authored inactive before pooling.");
        var statuses = participant.Statuses;
        for (int i = 0; i < statuses.Count; i++)
        {
            if (i == items.Count) items.Add(Instantiate(statusTemplate, statusRoot, false));
            items[i].Bind(statuses[i]);
        }
        for (int i = statuses.Count; i < items.Count; i++) items[i].Clear();
    }

    internal void Clear()
    {
        if (postureText != null) postureText.text = string.Empty;
        if (postureFill != null) postureFill.fillAmount = 0f;
        foreach (var item in items) if (item != null) item.Clear();
    }
}
