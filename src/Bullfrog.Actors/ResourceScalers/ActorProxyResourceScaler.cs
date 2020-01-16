using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.ResourceScalers
{
    public class ActorProxyResourceScaler : ResourceScaler
    {
        private readonly IResourceScalingActor _resourceScalingActor;

        public ActorProxyResourceScaler(IResourceScalingActor resourceScalingActor)
        {
            _resourceScalingActor = resourceScalingActor;
        }

        public override async Task<bool> ScaleIn()
            => GetResult(await _resourceScalingActor.ScaleIn());

        public override async Task<int?> ScaleOut(int throughput, DateTimeOffset endsAt)
            => GetResult(await _resourceScalingActor.ScaleOut(throughput, endsAt));

        private T GetResult<T>(ScalingResult<T> scalingResult)
            => scalingResult.ExceptionMessage == null
            ? scalingResult.Value
            : throw new ResourceScalingException(scalingResult.ExceptionMessage);
    }
}
