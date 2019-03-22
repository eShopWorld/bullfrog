using System;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Helpers
{
    class MyActorReminderData
    {
        public ActorId ActorId
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public TimeSpan DueTime
        {
            get;
            set;
        }

        public TimeSpan Period
        {
            get;
            set;
        }

        public byte[] State
        {
            get;
            set;
        }

        public DateTimeOffset Created
        {
            get;
            set;
        }

        public bool IsReadOnly
        {
            get;
            set;
        }

        public MyActorReminderData(ActorId actorId, IActorReminder reminder, DateTimeOffset created)
        {
            ActorId = actorId;
            Name = reminder.Name;
            DueTime = reminder.DueTime;
            Period = reminder.Period;
            State = reminder.State;
            Created = created;
        }
    }
}
