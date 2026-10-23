using TMPro;
using UnityEngine;

// One frozen status instance, not one status definition. Same-name statuses
// from separate sources remain separate rows and never re-read their source SO.
public sealed class BattleStatusInstanceView : MonoBehaviour
{
    [SerializeField] private TMP_Text displayName;
    [SerializeField] private TMP_Text duration;
    [SerializeField] private UnityEngine.UI.Image icon;

    internal void Bind(DRPGStatusRuntimeSnapshot status)
    {
        if (displayName != null)
            displayName.text = (string.IsNullOrEmpty(status.Spec.DisplayName) ? status.StatusId : status.Spec.DisplayName) + " ×" + status.Stacks;
        if (duration != null) duration.text = status.RemainingTurns.ToString();
        if (icon != null)
        {
            icon.sprite = status.Spec.Icon;
            icon.enabled = icon.sprite != null;
        }
        gameObject.SetActive(true);
    }

    internal void Clear()
    {
        if (displayName != null) displayName.text = string.Empty;
        if (duration != null) duration.text = string.Empty;
        if (icon != null) { icon.sprite = null; icon.enabled = false; }
        gameObject.SetActive(false);
    }
}
