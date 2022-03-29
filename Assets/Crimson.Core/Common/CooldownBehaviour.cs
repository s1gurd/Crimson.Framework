﻿using Crimson.Core.Components;
using Crimson.Core.Utils;
using System;

namespace Crimson.Core.Common
{
    public class CooldownBehaviour : TimerBaseBehaviour, IEnableable
    {
        public bool IsEnable { get; set; } = true;

        public void ApplyActionWithCooldown(float cooldownTime, Action action)
        {
            if (!IsEnable) return;

            action.Invoke();

            if (Math.Abs(cooldownTime) < 0.1f) return;

            StartTimer();
            Timer.TimedActions.AddAction(FinishTimer, cooldownTime);
        }

        public override void FinishTimer()
        {
            base.FinishTimer();
            IsEnable = true;
        }

        public override void StartTimer()
        {
            base.StartTimer();
            IsEnable = false;
        }
    }
}