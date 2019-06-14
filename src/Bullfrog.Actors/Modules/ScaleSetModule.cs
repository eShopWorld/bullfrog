using System;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Helpers;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;
using Eshopworld.Core;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;

namespace Bullfrog.Actors.Modules
{
    internal class ScaleSetModule : ScalingModule
    {
        private readonly Azure.IAuthenticated _authenticated;
        private readonly ScaleSetConfiguration _configuration;
        private readonly IScaleSetMonitor _scaleSetMonitor;
        private readonly IBigBrother _bigBrother;

        public ScaleSetModule(Azure.IAuthenticated authenticated, ScaleSetConfiguration configuration, IScaleSetMonitor scaleSetMonitor, IBigBrother bigBrother)
        {
            _authenticated = authenticated;
            _configuration = configuration;
            _scaleSetMonitor = scaleSetMonitor;
            _bigBrother = bigBrother;
        }

        public override async Task<int?> SetThroughput(int? newThroughput)
        {
            int expectedInstances;
            if (newThroughput.HasValue)
            {
                var instances = (int)(newThroughput.Value + (_configuration.ReservedInstances + 1) * _configuration.RequestsPerInstance - 1)
                    / _configuration.RequestsPerInstance;

                await UpdateProfile(_configuration, profile =>
                {
                    instances = Math.Min(profile.MaxInstanceCount,
                        Math.Max(instances, _configuration.MinInstanceCount));
                    var defaultInstances = Math.Min(profile.MaxInstanceCount,
                        Math.Max(instances, _configuration.DefaultInstanceCount));
                    return (instances, defaultInstances);
                });

                expectedInstances = instances;
            }
            else
            {
                await UpdateProfile(_configuration,
                    profile => (_configuration.MinInstanceCount, _configuration.DefaultInstanceCount));
                expectedInstances = _configuration.MinInstanceCount;
            }

            var workingInstances = await _scaleSetMonitor.GetNumberOfWorkingInstances(
                _configuration.LoadBalancerResourceId, _configuration.HealthPortPort);
            if (expectedInstances < workingInstances)
            {
                var usableInstances = Math.Max(workingInstances - _configuration.ReservedInstances, 0);
                return (int)(usableInstances * _configuration.RequestsPerInstance);
            }
            else
            {
                return null;
            }
        }

        private async Task UpdateProfile(
            ScaleSetConfiguration configuration,
            Func<IAutoscaleProfile, (int minInstance, int defaultInstances)> newInstanceCounts,
            CancellationToken cancellationToken = default)
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
