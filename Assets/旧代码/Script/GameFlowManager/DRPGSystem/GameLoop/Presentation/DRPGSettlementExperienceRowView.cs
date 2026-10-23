using TMPro;
using UnityEngine;

public sealed class DRPGSettlementExperienceRowView : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image portrait;
    [SerializeField] private TMP_Text characterName;
    [SerializeField] private TMP_Text experienceText;
    [SerializeField] private TMP_Text levelText;

    public void Bind(DRPGExperienceAward award)
    {
        DRPGCombatantProfileSO profile = award != null ? award.Profile : null;
        if (portrait != null)
        {
            portrait.sprite = profile != null ? profile.FullPortrait : null;
            portrait.enabled = portrait.sprite != null;
            portrait.preserveAspect = true;
        }
        characterName?.SetText(profile != null
            ? profile.DisplayName
            : "UNKNOWN");
        experienceText?.SetText(award != null
            ? "+" + award.Experience + " EXP"
            : "+0 EXP");
        if (levelText != null)
        {
            levelText.text = award != null && award.LevelAfter > award.LevelBefore
                ? "LV " + award.LevelBefore + "  >  " + award.LevelAfter
                : "LV " + (award != null ? award.LevelAfter : 1);
        }
    }
}
