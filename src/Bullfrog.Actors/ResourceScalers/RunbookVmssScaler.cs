using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Actors.ResourceScalers
{
    public class RunbookVmssScaler : ResourceScaler
    {
        private readonly ScaleSetConfiguration _configuration;
        private readonly string _scaleGroup;
        private readonly string _region;
        private readonly IActorProxyFactory _actorProxyFactory;

        public RunbookVmssScaler(RunbookVmssScalerConfiguration configuration, IActorProxyFactory actorProxyFactory)
        {
            _configuration = configuration.ScaleSet;
            _scaleGroup = configuration.ScaleGroup;
            _region = configuration.Region;
            _actorProxyFactory = actorProxyFactory;
        }

        public override async Task<bool> ScaleIn()
        {
            return await GetManager().ScaleIn();
        }

        public override async Task<int?> ScaleOut(int throughput, DateTimeOffset endsAt)
        {
            return await GetManager().ScaleOut(throughput, endsAt);
        }

        public IRunbookVmssScalingManager GetManager()
        {
            return _actorProxyFactory.GetRunbookVmssScalingManager(_scaleGroup, _region, _configuration.Name);
        }
    }
}
