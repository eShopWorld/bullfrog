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

        public async Task SetScale(int size, ScaleSetConfiguration configuration, CancellationToken cancellationToken)
        {
            await UpdateProfile(configuration, profile =>
            {
                if (size > profile.MaxInstanceCount)
                    size = profile.MaxInstanceCount;
                return (size, size);
            }, cancellationToken);
        }

        public async Task Reset(ScaleSetConfiguration configuration, CancellationToken cancellationToken)
        {
            await UpdateProfile(configuration, profile => (configuration.MinInstanceCount, configuration.DefaultInstanceCount), cancellationToken);
        }

        private async Task UpdateProfile(
            ScaleSetConfiguration configuration,
            Func<IAutoscaleProfile, (int minInstance, int defaultInstances)> newInstanceCounts,
            CancellationToken cancellationToken)
        {
            var parameters = new UpdateAutoscaleProfileParameters
            {
                AutoscaleSettingsResourceId = configuration.AutoscaleSettingsResourceId,
                ProfileName = configuration.ProfileName,
                NewInstanceCountCalculator = newInstanceCounts,
            };
            await _azure.UpdateAutoscaleProfile(parameters, cancellationToken);
       }
    }
}
