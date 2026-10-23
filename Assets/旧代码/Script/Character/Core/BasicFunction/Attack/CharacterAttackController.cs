using UnityEngine;

[DisallowMultipleComponent]
public class CharacterAttackController : MonoBehaviour, ICharacterSubsystem
{
    public enum AttackInputChannel
    {
        None,
        Hunter,
        Hound
    }

    [Header("Runtime References")]
    [SerializeField] private CharacterRoot characterRoot;

    private CharacterDataBus dataBus;
    private bool _isInitialized;

    private bool hunterAttackHeld;
    private bool houndAttackHeld;

    private bool lastHunterAttackHeld;
    private bool lastHoundAttackHeld;

    public bool HunterAttackHeld => hunterAttackHeld;
    public bool HoundAttackHeld => houndAttackHeld;
    public bool AnyAttackHeld => hunterAttackHeld || houndAttackHeld;

    public AttackInputChannel LastInputChannel { get; private set; } = AttackInputChannel.None;

    public void Initialize(CharacterRoot root)
    {
        if (_isInitialized)
            return;

        characterRoot = root;

        if (characterRoot != null)
        {
            dataBus = characterRoot.characterDataBus;
        }

        if (dataBus == null)
        {
            Debug.LogError("[CharacterAttackInputController] dataBus == null", this);
            return;
        }

        SyncToDataBus();

        _isInitialized = true;
    }

    private void Awake()
    {
        if (characterRoot == null)
            characterRoot = GetComponent<CharacterRoot>();
    }

    public void SetHunterAttackHeld(bool held)
    {
        hunterAttackHeld = held;

        if (held)
            LastInputChannel = AttackInputChannel.Hunter;
        else
            RefreshLastInputChannelAfterRelease();

        SyncToDataBus();
    }

    public void SetHoundAttackHeld(bool held)
    {
        houndAttackHeld = held;

        if (held)
            LastInputChannel = AttackInputChannel.Hound;
        else
            RefreshLastInputChannelAfterRelease();

        SyncToDataBus();
    }

    public void SetAttackHeld(AttackInputChannel channel, bool held)
    {
        switch (channel)
        {
            case AttackInputChannel.Hunter:
                SetHunterAttackHeld(held);
                break;

            case AttackInputChannel.Hound:
                SetHoundAttackHeld(held);
                break;

            case AttackInputChannel.None:
                if (!held)
                    ReleaseAll();
                break;
        }
    }

    public void ReleaseAll()
    {
        hunterAttackHeld = false;
        houndAttackHeld = false;
        LastInputChannel = AttackInputChannel.None;

        SyncToDataBus();
    }

    private void RefreshLastInputChannelAfterRelease()
    {
        if (houndAttackHeld)
        {
            LastInputChannel = AttackInputChannel.Hound;
            return;
        }

        if (hunterAttackHeld)
        {
            LastInputChannel = AttackInputChannel.Hunter;
            return;
        }

        LastInputChannel = AttackInputChannel.None;
    }

    private void SyncToDataBus()
    {
        if (dataBus == null)
            return;

        dataBus.hunterAttackHeld = hunterAttackHeld;
        dataBus.houndAttackHeld = houndAttackHeld;
    }

    private void OnDisable()
    {
        if (_isInitialized)
            ReleaseAll();
    }
}