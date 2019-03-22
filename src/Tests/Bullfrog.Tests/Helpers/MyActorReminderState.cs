using System;
using Microsoft.ServiceFabric.Actors.Runtime;
using ServiceFabric.Mocks;

namespace Helpers
{
    class MyActorReminderState : IActorReminderState
    {
        private readonly MyActorReminderData _reminderData;

        public TimeSpan RemainingDueTime { get; set; }

        public string Name => _reminderData.Name;

        public TimeSpan DueTime => _reminderData.DueTime;

        public TimeSpan Period => _reminderData.Period;

        public byte[] State => _reminderData.State;

        public DateTimeOffset NextExecution => _reminderData.Created + _reminderData.DueTime;

        public MyActorReminderState(MyActorReminderData reminder, DateTimeOffset utcNow)
        {
            _reminderData = reminder;
                RemainingDueTime = ComputeRemainingTime(utcNow, reminder.Created, reminder.DueTime);
        }

        public void Complete(MockActorReminderData reminder, DateTimeOffset currentLogicalTime, DateTimeOffset reminderCompletedTime)
        {
            RemainingDueTime = ComputeRemainingTime(currentLogicalTime, reminderCompletedTime, reminder.Period);
        }

        private static TimeSpan ComputeRemainingTime(DateTimeOffset currentLogicalTime, DateTimeOffset createdOrLastCompletedTime, TimeSpan dueTimeOrPeriod)
        {
            TimeSpan timeSpan1 = TimeSpan.Zero;
            if (currentLogicalTime > createdOrLastCompletedTime)
                timeSpan1 = currentLogicalTime - createdOrLastCompletedTime;
            TimeSpan timeSpan2 = TimeSpan.Zero;
            if (dueTimeOrPeriod > timeSpan1)
                timeSpan2 = dueTimeOrPeriod - timeSpan1;
            return timeSpan2;
        }
    }
}
