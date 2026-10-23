using UnityEngine;

[CreateAssetMenu(fileName = "WeaponAudioProfile",menuName = "ScriptableObject/Œ‰∆˜/Œ‰∆˜“Ù∆µ"
)]
public class WeaponAudioProfileSO : ScriptableObject
{
    [Header("Fire")]
    public AudioEventSO fire;
    public AudioEventSO fireMechanical;
    public AudioEventSO fireTail;

    [Header("Ammo")]
    public AudioEventSO empty;

    [Header("Reload")]
    public AudioEventSO reloadStart;
    public AudioEventSO reloadInsert;
    public AudioEventSO reloadCharge;
    public AudioEventSO reloadEnd;


    [Header("Impact")]
    public AudioEventSO defaultImpact;
    public AudioEventSO fleshImpact;
    public AudioEventSO metalImpact;
    public AudioEventSO concreteImpact;
    public AudioEventSO woodImpact;
    public AudioEventSO glassImpact;
}