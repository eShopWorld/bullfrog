using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Bullfrog.Actors
{
    public class RunbookVmssScalingManager : BullfrogActorBase, IRunbookVmssScalingManager
    {
        private readonly StateItem<RunbookVmssScalingManagerConfiguration> _configuration;
   
        public RunbookVmssScalingManager(ActorService actorService, ActorId actorId, IBigBrother bigBrother)
            : base(actorService, actorId, bigBrother)
        {
            _configuration = new StateItem<RunbookVmssScalingManagerConfiguration>(StateManager, "configuration");
        }

        async Task IRunbookVmssScalingManager.Configure(RunbookVmssScalingManagerConfiguration configuration)
        {
            await _configuration.Set(configuration);
        }

        async Task<bool> IRunbookVmssScalingManager.ScaleIn()
        {
            return true;
        }

        async Task<int?> IRunbookVmssScalingManager.ScaleOut(int throughput, DateTimeOffset endsAt)
        {
            return 0;
        }
    }
}
