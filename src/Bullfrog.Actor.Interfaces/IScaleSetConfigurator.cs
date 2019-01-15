using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces.Models;
using Microsoft.ServiceFabric.Actors;

namespace Bullfrog.Actor.Interfaces
{
    public interface IScaleSetConfigurator : IActor
    {
        Task Configure(ScaleSetConfiguration configuration, CancellationToken cancellationToken);

        Task SetScaleSetSize(int size, CancellationToken cancellationToken);
    }
}
