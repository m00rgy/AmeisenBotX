﻿using AmeisenBotX.Core.Data.Objects.WowObject;

namespace AmeisenBotX.Core.Statemachine.States
{
    public class StateDead : BasicState
    {
        public StateDead(AmeisenBotStateMachine stateMachine, AmeisenBotConfig config, WowInterface wowInterface) : base(stateMachine, config, wowInterface)
        {
        }

        public bool SetMapAndPosition { get; set; }

        public override void Enter()
        {
            SetMapAndPosition = false;
        }

        public override void Execute()
        {
            if (WowInterface.ObjectManager.Player.IsDead)
            {
                if (!SetMapAndPosition) // prevent re-setting the stuff in loading screen
                {
                    StateMachine.LastDiedMap = WowInterface.ObjectManager.MapId;

                    if (StateMachine.IsDungeonMap(StateMachine.LastDiedMap))
                    {
                        // when we died in a dungeon, we need to return to its portal
                        StateMachine.LastDiedPosition = WowInterface.DungeonEngine.DungeonProfile.WorldEntry;
                    }
                    else
                    {
                        StateMachine.LastDiedPosition = WowInterface.ObjectManager.Player.Position;
                    }
                }

                WowInterface.HookManager.ReleaseSpirit();
            }
            else if (WowInterface.HookManager.IsGhost(WowLuaUnit.Player))
            {
                StateMachine.SetState(BotState.Ghost);
            }
            else
            {
                StateMachine.SetState(BotState.Idle);
            }
        }

        public override void Exit()
        {
            SetMapAndPosition = false;
        }
    }
}