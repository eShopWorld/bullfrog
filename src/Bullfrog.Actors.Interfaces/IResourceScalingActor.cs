using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.ServiceFabric.Actors;

namespace Bullfrog.Actors.Interfaces
{
    /// <summary>
    /// An actor responsible for scaling a single resource.
    /// </summary>
    public interface IResourceScalingActor : IActor
    {
        /// <summary>
        /// Initializes the actor and defines its configuration.
        /// </summary>
        /// <param name="configuration">New configuration or null to deactivate the manager.</param>
        /// <returns></returns>
        Task Configure(ResourceScalingActorConfiguration configuration);

        /// <summary>
        /// Scales in the VMSS to minimal number of instances.
        /// </summary>
        /// <returns></returns>
        Task<bool> ScaleIn();

        /// <summary>
        /// Scales out the VMSS to the level necessary to handle requested throughput.
        /// </summary>
        /// <param name="throughput">The predicted throughput.</param>
        /// <param name="endsAt">The end of period of requested throughput.</param>
        /// <returns></returns>
        Task<int?> ScaleOut(int throughput, DateTimeOffset endsAt);
    }
}
