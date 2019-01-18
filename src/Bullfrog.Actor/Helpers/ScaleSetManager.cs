using System;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces.Models;
using Bullfrog.Common;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;

namespace Bullfrog.Actor.Helpers
{
    class ScaleSetManager : IScaleSetManager
    {
        private readonly IAzure _azure;

        public ScaleSetManager(IAzure azure)
        {
            _azure = azure;
        }

        public async Task<int> SetScale(int scale, ScaleSetConfiguration configuration, CancellationToken cancellationToken)
        {
            var instances = (scale + configuration.RequestsPerInstance - 1)
                / configuration.RequestsPerInstance;

            await UpdateProfile(configuration, profile =>
            {
                if (instances > profile.MaxInstanceCount)
                    instances = profile.MaxInstanceCount;
                return (instances, instances);
            }, cancellationToken);

            return instances;
        }

        public async Task<int> Reset(ScaleSetConfiguration configuration, CancellationToken cancellationToken)
        {
            await UpdateProfile(configuration, profile => (configuration.MinInstanceCount, configuration.DefaultInstanceCount), cancellationToken);
            return configuration.MinInstanceCount;
        }

        private async Task UpdateProfile(
            ScaleSetConfiguration configuration,
            Func<IAutoscaleProfile, (int minInstance, int defaultInstances)> newInstanceCounts,
            CancellationToken cancellationToken)
        {
            await _azure.UpdateAutoscaleProfile(
                configuration.AutoscaleSettingsResourceId,
                configuration.ProfileName,
                newInstanceCounts,
                forceUpdate: false,
                cancellationToken);
       }
    }
}
