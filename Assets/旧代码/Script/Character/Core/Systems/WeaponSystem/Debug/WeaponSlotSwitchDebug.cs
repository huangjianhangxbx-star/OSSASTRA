using UnityEngine;

public class WeaponSlotSwitchDebug :
    MonoBehaviour
{
    [SerializeField]
    private CharacterRoot characterRoot;

    private WeaponEquipStateMachine EquipStateMachine
    {
        get
        {
            if (characterRoot == null ||
                characterRoot.stateArbitrator == null)
            {
                return null;
            }

            return characterRoot
                .stateArbitrator
                .weaponEquipSM;
        }
    }

    [ContextMenu("Switch To Slot 1")]
    private void SwitchToSlot1()
    {
        EquipStateMachine?.RequestSwitchSlot(
            WeaponSlotId.Slot1
        );
    }

    [ContextMenu("Switch To Slot 2")]
    private void SwitchToSlot2()
    {
        EquipStateMachine?.RequestSwitchSlot(
            WeaponSlotId.Slot2
        );
    }

    [ContextMenu("Switch To Slot 3")]
    private void SwitchToSlot3()
    {
        EquipStateMachine?.RequestSwitchSlot(
            WeaponSlotId.Slot3
        );
    }

    [ContextMenu("Switch To Next Slot")]
    private void SwitchToNextSlot()
    {
        EquipStateMachine?.RequestSwitchToNextSlot();
    }
}