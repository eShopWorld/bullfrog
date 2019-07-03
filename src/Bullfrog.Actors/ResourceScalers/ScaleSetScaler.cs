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

namespace Bullfrog.Actors.ResourceScalers
{
    public class ScaleSetScaler : ResourceScaler
    {
        private readonly Azure.IAuthenticated _authenticated;
        private readonly ScaleSetConfiguration _configuration;
        private readonly ScaleSetMonitor _scaleSetMonitor;
        private readonly IBigBrother _bigBrother;

        public ScaleSetScaler(Azure.IAuthenticated authenticated, ScaleSetConfiguration configuration, ScaleSetMonitor scaleSetMonitor, IBigBrother bigBrother)
        {
            _authenticated = authenticated;
            _configuration = configuration;
            _scaleSetMonitor = scaleSetMonitor;
            _bigBrother = bigBrother;
        }

        public override async Task<int?> ScaleIn()
        {
            await LogAzureCallDuration(_bigBrother, "RemoveBullfrogProfile", _configuration.AutoscaleSettingsResourceId, async () =>
            {
                var azure = _authenticated.WithSubscriptionFor(_configuration.AutoscaleSettingsResourceId);
                await azure.RemoveBullfrogProfile(_configuration.AutoscaleSettingsResourceId);
            });

            return 0;
        }

        public override async Task<int?> ScaleOut(int throughput, DateTimeOffset endsAt)
        {
            var instances = (int)(throughput + (_configuration.ReservedInstances + 1) * _configuration.RequestsPerInstance - 1)
                 / _configuration.RequestsPerInstance;

            await LogAzureCallDuration(_bigBrother, "SaveBullfrogProfile", _configuration.AutoscaleSettingsResourceId, async () =>
            {
                var azure = _authenticated.WithSubscriptionFor(_configuration.AutoscaleSettingsResourceId);
                await azure.SaveBullfrogProfile(_configuration.AutoscaleSettingsResourceId, _configuration.ProfileName,
                    profile =>
                    {
                        instances = Math.Min(profile.MaxInstanceCount,
                            Math.Max(instances, _configuration.MinInstanceCount));
                        var defaultInstances = Math.Min(profile.MaxInstanceCount,
                            Math.Max(instances, _configuration.DefaultInstanceCount));
                        return (instances, defaultInstances);
                    });
            });

            var workingInstances = await _scaleSetMonitor.GetNumberOfWorkingInstances(
               _configuration.LoadBalancerResourceId, _configuration.HealthPortPort);
            if (instances < workingInstances)
            {
                var usableInstances = Math.Max(workingInstances - _configuration.ReservedInstances, 0);
                return (int)(usableInstances * _configuration.RequestsPerInstance);
            }
            else
                return null;
        }

        private static async Task LogAzureCallDuration(IBigBrother bigBrother, string operation, string resourceId, Func<Task> action)
        {
            var durationEvent = new AzureOperationDurationEvent
            {
                Operation = operation,
                ResourceId = resourceId,
            };
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                durationEvent.ExceptionMessage = ex.Message;
                bigBrother.Publish(durationEvent);

                throw;
            }

            bigBrother.Publish(durationEvent);
        }
    }
}
