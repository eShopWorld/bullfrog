using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.Helpers
{
    public interface IScaleSetManager
    {
        Task<int> SetScale(int scale, ScaleSetConfiguration configuration, CancellationToken cancellationToken = default);

        Task<int> Reset(ScaleSetConfiguration configuration, CancellationToken cancellationToken = default);
    }
}
