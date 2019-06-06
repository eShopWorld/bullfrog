using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace Bullfrog.Actors.Interfaces
{
    public interface ICosmosManagementActor : IActor
    {
        /// <summary>
        /// Attempts to set the throughput to the specified value.
        /// </summary>
        /// <param name="throughput">The new throughput value.</param>
        /// <returns>If the throughput has been changed synchronously it the new throughput (might be different than requested) is returned. Otherwise returns null.</returns>
        Task<int?> SetThroughput(int throughput);

        /// <summary>
        /// Resets the throughput to the lowest level.
        /// </summary>
        /// <returns>If the throughput has been changed synchronously it the new throughput is returned. Otherwise returns null.</returns>
        Task<int?> ResetThroughput();

        Task SetConfiguration(Models.CosmosConfiguration cosmosConfiguration, ActorId ownerActorId);
    }
}
