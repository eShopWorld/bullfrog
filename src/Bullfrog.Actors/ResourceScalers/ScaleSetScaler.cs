using System;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;
using Bullfrog.Common.Helpers;
using Bullfrog.Common.Telemetry;
using Eshopworld.Core;
using Microsoft.Azure.Management.Fluent;

namespace Bullfrog.Actors.ResourceScalers
{
    public class ScaleSetScaler : ResourceScaler
    {
        private readonly Azure.IAuthenticated _authenticated;
        private readonly ScaleSetConfiguration _configuration;
        private readonly ScaleSetMonitor _scaleSetMonitor;
        private readonly IDateTimeProvider _dateTime;
        private readonly IBigBrother _bigBrother;

        public ScaleSetScaler(Azure.IAuthenticated authenticated, ScaleSetConfiguration configuration, ScaleSetMonitor scaleSetMonitor, IDateTimeProvider dateTime, IBigBrother bigBrother)
        {
            _authenticated = authenticated;
            _configuration = configuration;
            _scaleSetMonitor = scaleSetMonitor;
            _dateTime = dateTime;
            _bigBrother = bigBrother;
        }

        public override async Task<bool> ScaleIn()
        {
            var profile = await _bigBrother.LogAzureCallDuration("RemoveBullfrogProfile", _configuration.AutoscaleSettingsResourceId, async () =>
            {
                var azure = _authenticated.WithSubscriptionFor(_configuration.AutoscaleSettingsResourceId);
                return await azure.RemoveBullfrogProfile(_configuration.AutoscaleSettingsResourceId);
            });

            _bigBrother.Publish(new ScaleSetReset
            {
                ScalerName = _configuration.Name,
                ConfiguredInstances = profile.Profiles[_configuration.ProfileName].MinInstanceCount,
            });

            return true;
        }

        public override async Task<int?> ScaleOut(int throughput, DateTimeOffset endsAt)
        {
            var instances = (int)(throughput + (_configuration.ReservedInstances + 1) * _configuration.RequestsPerInstance - 1)
                 / _configuration.RequestsPerInstance;

            if (instances < _configuration.MinInstanceCount)
                instances = _configuration.MinInstanceCount.Value;

            var ssConfiguredInstances = await _bigBrother.LogAzureCallDuration("SaveBullfrogProfile", _configuration.AutoscaleSettingsResourceId, async () =>
            {
                var azure = _authenticated.WithSubscriptionFor(_configuration.AutoscaleSettingsResourceId);
                var (configuredInstances, profileChanged) = await azure.SaveBullfrogProfile(_configuration.AutoscaleSettingsResourceId, _configuration.ProfileName, instances,
                      _dateTime.UtcNow, endsAt);

                if (profileChanged)
                {
                    _bigBrother.Publish(new ScaleSetModified
                    {
                        ScalerName = _configuration.Name,
                        RequestedInstances = instances,
                        ConfiguredInstances = configuredInstances,
                        RequestedThroughput = throughput,
                    });
                }

                return configuredInstances;
            });

            var workingInstances = await _bigBrother.LogAzureCallDuration("GetNumberOfInstances", _configuration.LoadBalancerResourceId,
                async () => await _scaleSetMonitor.GetNumberOfWorkingInstances(
                    _configuration.LoadBalancerResourceId, _configuration.HealthPortPort));

            var usableInstances = Math.Max(workingInstances - _configuration.ReservedInstances, 0);
            var availableThroughput = (int)(usableInstances * _configuration.RequestsPerInstance);

            _bigBrother.Publish(new ScaleSetThroughput
            {
                ScalerName = _configuration.Name,
                RequestedThroughput = throughput,
                RequiredInstances = instances,
                ConfiguredInstances = ssConfiguredInstances,
                WorkingInstances = workingInstances,
                AvailableThroughput = availableThroughput,
            });

            if (instances < workingInstances)
            {
                return availableThroughput;
            }
            else
                return null;
        }
    }
}
