using System;
using System.Threading.Tasks;
using Bullfrog.Common;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;

namespace Bullfrog.Actors.Helpers
{
    internal class AutoscaleSettingsHandlerFactory : IAutoscaleSettingsHandlerFactory
    {
        private readonly Azure.IAuthenticated _authenticated;

        public AutoscaleSettingsHandlerFactory(Azure.IAuthenticated authenticated)
        {
            _authenticated = authenticated;
        }

        public IAutoscaleSettingsHandler CreateHandler(string autoscaleSettingsResourceId, string defaultProfileName)
        {
            var azure = _authenticated.WithSubscriptionFor(autoscaleSettingsResourceId);
            return new AutoscaleSettingsHandler(azure, autoscaleSettingsResourceId, defaultProfileName);
        }
    }

    internal class AutoscaleSettingsHandler : IAutoscaleSettingsHandler
    {
        private readonly IAzure _azure;
        private readonly string _autoscaleSettingsResourceId;
        private readonly string _defaultProfileName;

        public AutoscaleSettingsHandler(IAzure azure, string autoscaleSettingsResourceId, string defaultProfileName)
        {
            _azure = azure;
            _autoscaleSettingsResourceId = autoscaleSettingsResourceId;
            _defaultProfileName = defaultProfileName;
        }

        public async Task<AutoscaleSettingsSummary> Read()
        {
            var autoscale = await _azure.AutoscaleSettings.ValidateAccessAsync(_autoscaleSettingsResourceId);
            return CreateView(autoscale);
        }

        public async Task<(AutoscaleSettingsSummary autoscaleSettings, bool chaged)> RemoveBullfrogProfile()
        {
            var result = await _azure.RemoveBullfrogProfile(_autoscaleSettingsResourceId);

            return (CreateView(result.autoscaleSettings), result.chagned);
        }

        public async Task<(AutoscaleSettingsSummary autoscaleSettings, bool chaged)> UpdateBullfrogProfile(BullfrogChange bullfrogProfile)
        {
            var (_, profileChanged, updatedSettings) = await _azure.SaveBullfrogProfile(
                 _autoscaleSettingsResourceId,
                 _defaultProfileName,
                 bullfrogProfile.Minimum,
                 bullfrogProfile.Starts,
                 bullfrogProfile.Ends);

            return (CreateView(updatedSettings), profileChanged);
        }

        private AutoscaleSettingsSummary CreateView(IAutoscaleSetting autoscale)
        {
            if (!autoscale.Profiles.TryGetValue(_defaultProfileName, out var defaultProfile))
                throw new BullfrogException($"Cannot find {_defaultProfileName} profile in autoscale settings {_autoscaleSettingsResourceId}.");

            autoscale.Profiles.TryGetValue(AzureFluentExtensions.BullfrogProfileName, out var bullfrogProfile);

            return new AutoscaleSettingsSummary
            {
                DefaultMinimum = defaultProfile.MinInstanceCount,
                DefaultMaximum = defaultProfile.MaxInstanceCount,
                BullfrogProfile = bullfrogProfile == null ? null : new BullfrogProfileSummary
                {
                    Minimum = bullfrogProfile.MinInstanceCount,
                    Maximum = bullfrogProfile.MaxInstanceCount,
                    Starts = bullfrogProfile.FixedDateSchedule?.Start ?? DateTimeOffset.MinValue,
                    Ends = bullfrogProfile.FixedDateSchedule?.End ?? DateTimeOffset.MinValue,
                },
            };
        }
    }
}
