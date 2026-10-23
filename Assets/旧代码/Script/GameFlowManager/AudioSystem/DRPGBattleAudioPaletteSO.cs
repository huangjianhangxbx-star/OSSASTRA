using System;
using System.Collections.Generic;
using UnityEngine;

public enum BattleAudioPlaybackChannel
{
    Sfx = 0,
    Ui = 1,
    Voice = 2,
    Music = 3
}

[Serializable]
public sealed class DRPGBattleSkillAudioOverride
{
    [SerializeField] private string skillId;
    [SerializeField] private AudioEventSO attack;
    [SerializeField] private AudioEventSO hit;

    public DRPGBattleSkillAudioOverride(
        string skillId,
        AudioEventSO attack,
        AudioEventSO hit)
    {
        this.skillId = skillId ?? string.Empty;
        this.attack = attack;
        this.hit = hit;
    }

    public string SkillId => skillId ?? string.Empty;
    public AudioEventSO Attack => attack;
    public AudioEventSO Hit => hit;
}

[Serializable]
public sealed class DRPGBattleVoiceAudioEntry
{
    [SerializeField] private string semanticCueId;
    [SerializeField] private AudioEventSO audioEvent;

    public DRPGBattleVoiceAudioEntry(
        string semanticCueId,
        AudioEventSO audioEvent)
    {
        this.semanticCueId = semanticCueId ?? string.Empty;
        this.audioEvent = audioEvent;
    }

    public string SemanticCueId => semanticCueId ?? string.Empty;
    public AudioEventSO AudioEvent => audioEvent;
}

[CreateAssetMenu(
    fileName = "DRPGBattleAudioPalette",
    menuName = "ANKALUTE/Audio/DRPG Battle Audio Palette")]
public sealed class DRPGBattleAudioPaletteSO : ScriptableObject
{
    [Header("Music")]
    [SerializeField] private AudioEventSO battleBgm;

    [Header("UI")]
    [SerializeField] private AudioEventSO skillSelected;

    [Header("Combat")]
    [SerializeField] private AudioEventSO defaultAttack;
    [SerializeField] private AudioEventSO defaultHit;
    [SerializeField] private AudioEventSO breakEnter;
    [SerializeField] private AudioEventSO breakExit;
    [SerializeField] private AudioEventSO victory;
    [SerializeField] private AudioEventSO defeat;

    [Header("Interactive combat")]
    [SerializeField] private AudioEventSO specialWarning;
    [SerializeField] private AudioEventSO interactiveWindowOpen;
    [SerializeField] private AudioEventSO guard;
    [SerializeField] private AudioEventSO perfectGuard;
    [SerializeField] private AudioEventSO miss;
    [SerializeField] private AudioEventSO weakHit;
    [SerializeField] private AudioEventSO interactiveWindowClose;

    [Header("Overrides")]
    [SerializeField] private List<DRPGBattleSkillAudioOverride> skillOverrides =
        new List<DRPGBattleSkillAudioOverride>();
    [SerializeField] private List<DRPGBattleVoiceAudioEntry> voiceCues =
        new List<DRPGBattleVoiceAudioEntry>();

    public bool TryResolve(
        BattleAudioCueRequest request,
        out AudioEventSO audioEvent,
        out BattleAudioPlaybackChannel channel)
    {
        audioEvent = null;
        channel = BattleAudioPlaybackChannel.Sfx;

        switch (request.Cue)
        {
            case BattleAudioCue.BattleBgm:
                audioEvent = battleBgm;
                channel = BattleAudioPlaybackChannel.Music;
                break;

            case BattleAudioCue.SkillSelected:
                audioEvent = skillSelected;
                channel = BattleAudioPlaybackChannel.Ui;
                break;

            case BattleAudioCue.Attack:
                audioEvent = ResolveSkillOverride(request.SkillId, true) ??
                    defaultAttack;
                break;

            case BattleAudioCue.Hit:
                audioEvent = ResolveSkillOverride(request.SkillId, false) ??
                    defaultHit;
                break;

            case BattleAudioCue.Voice:
                audioEvent = ResolveVoice(request.SemanticCueId);
                channel = BattleAudioPlaybackChannel.Voice;
                break;

            case BattleAudioCue.BreakEnter:
                audioEvent = breakEnter;
                break;

            case BattleAudioCue.BreakExit:
                audioEvent = breakExit;
                break;

            case BattleAudioCue.Victory:
                audioEvent = victory;
                channel = BattleAudioPlaybackChannel.Ui;
                break;

            case BattleAudioCue.Defeat:
                audioEvent = defeat;
                channel = BattleAudioPlaybackChannel.Ui;
                break;

            case BattleAudioCue.SpecialWarning:
                audioEvent = specialWarning;
                channel = BattleAudioPlaybackChannel.Ui;
                break;

            case BattleAudioCue.InteractiveWindowOpen:
                audioEvent = interactiveWindowOpen;
                channel = BattleAudioPlaybackChannel.Ui;
                break;

            case BattleAudioCue.Guard:
                audioEvent = guard;
                break;

            case BattleAudioCue.PerfectGuard:
                audioEvent = perfectGuard;
                break;

            case BattleAudioCue.Miss:
                audioEvent = miss;
                channel = BattleAudioPlaybackChannel.Ui;
                break;

            case BattleAudioCue.WeakHit:
                audioEvent = weakHit;
                break;

            case BattleAudioCue.InteractiveWindowClose:
                audioEvent = interactiveWindowClose;
                channel = BattleAudioPlaybackChannel.Ui;
                break;
        }

        return audioEvent != null;
    }

    private AudioEventSO ResolveSkillOverride(string skillId, bool attackCue)
    {
        if (string.IsNullOrWhiteSpace(skillId) || skillOverrides == null)
            return null;

        for (int i = 0; i < skillOverrides.Count; i++)
        {
            DRPGBattleSkillAudioOverride entry = skillOverrides[i];
            if (entry == null || !string.Equals(
                    entry.SkillId,
                    skillId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            return attackCue ? entry.Attack : entry.Hit;
        }

        return null;
    }

    private AudioEventSO ResolveVoice(string semanticCueId)
    {
        if (string.IsNullOrWhiteSpace(semanticCueId) || voiceCues == null)
            return null;

        for (int i = 0; i < voiceCues.Count; i++)
        {
            DRPGBattleVoiceAudioEntry entry = voiceCues[i];
            if (entry != null && string.Equals(
                    entry.SemanticCueId,
                    semanticCueId,
                    StringComparison.Ordinal))
            {
                return entry.AudioEvent;
            }
        }

        return null;
    }
}
