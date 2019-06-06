using System;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;
using Eshopworld.Core;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;

namespace Bullfrog.Actors.Helpers
{
    internal class ScaleSetManager : IScaleSetManager
    {
        private readonly Azure.IAuthenticated _authenticated;
        private readonly IBigBrother _bigBrother;

        public ScaleSetManager(Azure.IAuthenticated authenticated, IBigBrother bigBrother)
        {
            _authenticated = authenticated;
            _bigBrother = bigBrother;
        }

        public async Task<int> SetScale(int scale, ScaleSetConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var instances = (int)(scale + (configuration.ReservedInstances + 1) * configuration.RequestsPerInstance - 1)
                / configuration.RequestsPerInstance;

            await UpdateProfile(configuration, profile =>
            {
                instances = Math.Min(profile.MaxInstanceCount,
                    Math.Max(instances, configuration.MinInstanceCount));
                var defaultInstances = Math.Min(profile.MaxInstanceCount,
                    Math.Max(instances, configuration.DefaultInstanceCount));
                return (instances, defaultInstances);
            }, cancellationToken);

            return instances;
        }

        public async Task<int> Reset(ScaleSetConfiguration configuration, CancellationToken cancellationToken = default)
        {
            await UpdateProfile(configuration, profile => (configuration.MinInstanceCount, configuration.DefaultInstanceCount), cancellationToken);
            return configuration.MinInstanceCount;
        }

        private async Task UpdateProfile(
            ScaleSetConfiguration configuration,
            Func<IAutoscaleProfile, (int minInstance, int defaultInstances)> newInstanceCounts,
            CancellationToken cancellationToken)
        {
            var listResourcesOperation = new AzureOperationDurationEvent
            {
                Operation = "UpdateAutoscale",
                ResourceId = configuration.AutoscaleSettingsResourceId,
            };

            var azure = _authenticated.WithSubscriptionFor(configuration.AutoscaleSettingsResourceId);
            await azure.UpdateAutoscaleProfile(
                configuration.AutoscaleSettingsResourceId,
                configuration.ProfileName,
                newInstanceCounts,
                forceUpdate: false,
                cancellationToken);
            _bigBrother.Publish(listResourcesOperation);
        }
    }
}
