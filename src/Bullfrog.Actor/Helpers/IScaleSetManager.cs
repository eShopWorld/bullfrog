using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces.Models;

namespace Bullfrog.Actor.Helpers
{
    public interface IScaleSetManager
    {
        Task<int> SetScale(int scale, ScaleSetConfiguration configuration, CancellationToken cancellationToken);

        Task<int> Reset(ScaleSetConfiguration configuration, CancellationToken cancellationToken);
    }
}
