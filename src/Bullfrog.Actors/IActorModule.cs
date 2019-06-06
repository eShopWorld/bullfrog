using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Bullfrog.Actors
{
    interface IActorModule
    {
        void InitHost(IActorModuleHost host);

        Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period);
    }

    interface IActorModuleHost
    {
        IActorStateManager StateManager { get; }

        Task RegisterReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period);
    }
}
