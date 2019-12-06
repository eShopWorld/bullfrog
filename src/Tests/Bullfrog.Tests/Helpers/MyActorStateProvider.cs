﻿using Bullfrog.Common;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Helpers
{
    class MyActorStateProvider : MockActorStateProvider
    {
        private readonly IDateTimeProvider _dateTimeProvider;

        public MyActorStateProvider(IDateTimeProvider dateTimeProvider)
        {
            _dateTimeProvider = dateTimeProvider;
        }

        protected override IActorReminderState CreateReminderState(ActorId actorId, IActorReminder reminder)
        {
            var utcNow = _dateTimeProvider.UtcNow;
            return new MyActorReminderState(new MyActorReminderData(actorId, reminder, utcNow), utcNow);
        }
    }
}
