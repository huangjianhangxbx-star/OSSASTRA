using System;
using System.Collections.Generic;

namespace Ossastra.Characters
{
    public enum PostureInputCommand
    {
        None, MainHand, Offhand, Jump, Dodge, SkillOne, SkillTwo, SkillThree, WeaponMode,
        SwitchCharacter, Heal, Interact, QuickSlotOne, QuickSlotTwo, QuickSlotThree, QuickSlotFour
    }

    [Flags]
    internal enum PosturePadButtons
    {
        None = 0, X = 1, Y = 2, B = 4, A = 8, LeftShoulder = 16, RightShoulder = 32,
        LeftTrigger = 64, RightTrigger = 128, Up = 256
    }

    internal readonly struct PosturePadEdge
    {
        public readonly PosturePadButtons Button;
        public readonly PostureInputCommand Command;
        public readonly bool Pressed;
        public readonly int UpdateIndex;

        public PosturePadEdge(PosturePadButtons button, PostureInputCommand command, bool pressed, int updateIndex)
        {
            Button = button;
            Command = command;
            Pressed = pressed;
            UpdateIndex = updateIndex;
        }
    }

    internal sealed class PostureInputGestures
    {
        private const PosturePadButtons Shoulders = PosturePadButtons.LeftShoulder | PosturePadButtons.RightShoulder;
        private const PosturePadButtons Triggers = PosturePadButtons.LeftTrigger | PosturePadButtons.RightTrigger;
        private static readonly PosturePadButtons[] OwnedButtons =
        {
            PosturePadButtons.X, PosturePadButtons.Y, PosturePadButtons.B, PosturePadButtons.A,
            PosturePadButtons.RightTrigger, PosturePadButtons.Up
        };
        private readonly PostureInputCommand[] ownership = new PostureInputCommand[OwnedButtons.Length];
        private PosturePadButtons previous;
        private bool shoulderLocked, triggerLocked;

        public void Reset(PosturePadButtons state, bool preserveJump = false)
        {
            previous = state;
            shoulderLocked = (state & Shoulders) != 0;
            triggerLocked = (state & Triggers) != 0;
            for (int index = 0; index < ownership.Length; index++)
                if (!preserveJump || ownership[index] != PostureInputCommand.Jump)
                    ownership[index] = PostureInputCommand.None;
        }

        public void Resolve(IReadOnlyList<PosturePadButtons> updates, bool accept, List<PosturePadEdge> edges)
        {
            for (int update = 0; update < updates.Count; update++)
            {
                var state = updates[update];
                if ((state & Shoulders) == 0) shoulderLocked = false;
                if ((state & Triggers) == 0) triggerLocked = false;
                if (!shoulderLocked && (state & Shoulders) == Shoulders)
                {
                    shoulderLocked = true;
                    if (accept) edges.Add(new PosturePadEdge(Shoulders, PostureInputCommand.SwitchCharacter, true, update));
                }
                if (!triggerLocked && (state & Triggers) == Triggers)
                {
                    triggerLocked = true;
                    if (accept) edges.Add(new PosturePadEdge(Triggers, PostureInputCommand.WeaponMode, true, update));
                }

                var changed = previous ^ state;
                for (int index = 0; index < OwnedButtons.Length; index++)
                {
                    var button = OwnedButtons[index];
                    if ((changed & button) == 0) continue;
                    bool pressed = (state & button) != 0;
                    if (pressed)
                        ownership[index] = accept
                            ? SelectCommand(button, updates, update, shoulderLocked, triggerLocked)
                            : PostureInputCommand.None;
                    var command = ownership[index];
                    if (command != PostureInputCommand.None)
                        edges.Add(new PosturePadEdge(button, command, pressed, update));
                    if (!pressed) ownership[index] = PostureInputCommand.None;
                }
                previous = state;
            }
        }

        private static PostureInputCommand SelectCommand(PosturePadButtons button,
            IReadOnlyList<PosturePadButtons> updates, int index, bool shoulderLocked, bool triggerLocked)
        {
            if (button == PosturePadButtons.Up) return PostureInputCommand.Interact;
            if (button == PosturePadButtons.RightTrigger)
                return triggerLocked || FindModifier(button, Triggers, updates, index) == Triggers
                    ? PostureInputCommand.None : PostureInputCommand.Offhand;
            if (shoulderLocked) return PostureInputCommand.None;
            var modifier = FindModifier(button, Shoulders, updates, index);
            if (modifier != PosturePadButtons.None && FormsSwitchInSameShoulderGesture(updates, index))
                return PostureInputCommand.None;
            if (modifier == PosturePadButtons.LeftShoulder)
                return button switch
                {
                    PosturePadButtons.Y => PostureInputCommand.SkillTwo,
                    PosturePadButtons.B => PostureInputCommand.SkillThree,
                    PosturePadButtons.A => PostureInputCommand.Heal,
                    _ => PostureInputCommand.None
                };
            if (modifier == PosturePadButtons.RightShoulder)
                return button switch
                {
                    PosturePadButtons.X => PostureInputCommand.QuickSlotOne,
                    PosturePadButtons.Y => PostureInputCommand.QuickSlotTwo,
                    PosturePadButtons.B => PostureInputCommand.QuickSlotThree,
                    _ => PostureInputCommand.QuickSlotFour
                };
            return button switch
            {
                PosturePadButtons.X => PostureInputCommand.MainHand,
                PosturePadButtons.Y => PostureInputCommand.SkillOne,
                PosturePadButtons.B => PostureInputCommand.Dodge,
                _ => PostureInputCommand.Jump
            };
        }

        private static PosturePadButtons FindModifier(PosturePadButtons button, PosturePadButtons mask,
            IReadOnlyList<PosturePadButtons> updates, int index)
        {
            // Allow a modifier in the same Input System update while the press is still down.
            PosturePadButtons first = PosturePadButtons.None;
            for (; index < updates.Count && (updates[index] & button) != 0; index++)
            {
                var modifier = updates[index] & mask;
                if (modifier == mask) return modifier;
                if (first == PosturePadButtons.None) first = modifier;
            }
            return first;
        }

        private static bool FormsSwitchInSameShoulderGesture(IReadOnlyList<PosturePadButtons> updates, int index)
        {
            // The face may already have been released when the other shoulder joins this batch.
            while (index < updates.Count && (updates[index] & Shoulders) == 0) index++;
            for (; index < updates.Count && (updates[index] & Shoulders) != 0; index++)
                if ((updates[index] & Shoulders) == Shoulders) return true;
            return false;
        }
    }
}
