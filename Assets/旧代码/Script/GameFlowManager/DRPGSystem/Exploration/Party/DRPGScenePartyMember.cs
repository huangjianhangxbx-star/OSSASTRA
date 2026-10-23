using UnityEngine;

/// <summary>
/// Stable scene identity for a party member reused by exploration and battle.
/// </summary>
[DisallowMultipleComponent]
public sealed class DRPGScenePartyMember : MonoBehaviour
{
    [SerializeField] private string stableId;
    [SerializeField] private CharacterRoot character;
    [SerializeField] private BattleWorldActorView battleView;

    public string StableId => stableId;
    public CharacterRoot Character => character;
    public BattleWorldActorView View => battleView;
    public bool IsIncapacitated { get; private set; }

    public void ConfigureForRuntime(
        string id,
        CharacterRoot characterRoot,
        BattleWorldActorView actorView)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new System.ArgumentException(
                "A scene party member requires a stable id.", nameof(id));
        stableId = id.Trim();
        character = characterRoot;
        battleView = actorView;
    }

    public void SetIncapacitated(bool value)
    {
        IsIncapacitated = value;
        GameObject visualObject = character != null
            ? character.gameObject
            : battleView != null ? battleView.gameObject : null;
        if (visualObject != null)
            visualObject.SetActive(!value);
    }

    private void OnValidate()
    {
        stableId = stableId != null ? stableId.Trim() : string.Empty;
        if (character == null)
            character = GetComponent<CharacterRoot>();
        if (battleView == null)
            battleView = GetComponent<BattleWorldActorView>();
    }
}
