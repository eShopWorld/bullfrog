using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces.Models;

namespace Bullfrog.Actor.Helpers
{
    public interface IScaleSetManager
    {
        Task SetScale(int size, ScaleSetConfiguration configuration, CancellationToken cancellationToken);

        Task Reset(ScaleSetConfiguration configuration, CancellationToken cancellationToken);
    }
}
