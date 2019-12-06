using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;

namespace Bullfrog.Actors.ResourceScalers
{
    public class ActorProxyResourceScaler : ResourceScaler
    {
        private readonly IResourceScalingActor _resourceScalingActor;

        public ActorProxyResourceScaler(IResourceScalingActor resourceScalingActor)
        {
            _resourceScalingActor = resourceScalingActor;
        }

        public override Task<bool> ScaleIn() => _resourceScalingActor.ScaleIn();
        
        public override Task<int?> ScaleOut(int throughput, DateTimeOffset endsAt)
            => _resourceScalingActor.ScaleOut(throughput, endsAt);
    }
}
