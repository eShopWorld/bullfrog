using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.ServiceFabric.Actors;

namespace Bullfrog.Actors.Interfaces
{
    public interface IScaleSetConfigurator : IActor
    {
        Task Configure(ScaleSetConfiguration configuration, CancellationToken cancellationToken);

        Task SetScaleSetSize(int size, CancellationToken cancellationToken);
    }
}
