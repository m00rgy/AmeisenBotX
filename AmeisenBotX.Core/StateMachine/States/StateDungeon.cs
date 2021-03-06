﻿namespace AmeisenBotX.Core.Statemachine.States
{
    public class StateDungeon : BasicState
    {
        public StateDungeon(AmeisenBotStateMachine stateMachine, AmeisenBotConfig config, WowInterface wowInterface) : base(stateMachine, config, wowInterface)
        {
        }

        public override void Enter()
        {
            WowInterface.MovementEngine.Reset();
            WowInterface.DungeonEngine.Reset();
            StateMachine.OnStateOverride += StateMachine_OnStateOverride;
        }

        public override void Execute()
        {
            if (StateMachine.IsDungeonMap(WowInterface.ObjectManager.MapId))
            {
                WowInterface.DungeonEngine.Execute();
            }
            else
            {
                StateMachine.SetState(BotState.Idle);
            }
        }

        public override void Exit()
        {
            StateMachine.OnStateOverride -= StateMachine_OnStateOverride;
            WowInterface.MovementEngine.Reset();
            WowInterface.DungeonEngine.Reset();
        }

        private void StateMachine_OnStateOverride(BotState botState)
        {
            if (botState == BotState.Dead)
            {
                WowInterface.DungeonEngine.OnDeath();
            }
        }
    }
}