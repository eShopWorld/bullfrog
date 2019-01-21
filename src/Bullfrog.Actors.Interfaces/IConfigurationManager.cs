using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.ServiceFabric.Actors;

namespace Bullfrog.Actors.Interfaces
{
    public interface IConfigurationManager : IActor
    {
        Task<Dictionary<string, string[]>> ConfigureScaleGroup(string name, ScaleGroupDefinition definition, CancellationToken cancellationToken);

        Task<ScaleGroupDefinition> GetScaleGroupConfiguration(string name, CancellationToken cancellationToken);

        Task<List<string>> ListConfiguredScaleGroup(CancellationToken cancellationToken);
    }
}
