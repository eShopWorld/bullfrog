using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.CosmosDB.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent.Models;

namespace Bullfrog.Common
{
    // TODO: rename
    public static class IAzureExtensions
    {
        public static async Task<IAzure> UpdateAutoscaleProfile(this IAzure azure, UpdateAutoscaleProfileParameters parameters, CancellationToken cancellationToken = default)
        {
            var autoscale = await azure.AutoscaleSettings.ValidateAccessAsync(parameters.AutoscaleSettingsResourceId, cancellationToken);
            if (!autoscale.Profiles.TryGetValue(parameters.ProfileName, out var profile))
            {
                throw new Exception($"The profile {parameters.ProfileName} has not been found in the autoscale settings {parameters.AutoscaleSettingsResourceId}.");
            }

            var (minInstance, defaultInstances) = parameters.NewInstanceCountCalculator(profile);

            if (parameters.ForceUpdate
                || profile.MinInstanceCount != minInstance
                || profile.DefaultInstanceCount != defaultInstances)
            {
                await autoscale.Update()
                    .UpdateAutoscaleProfile(parameters.ProfileName)
                    .WithMetricBasedScale(minInstance, profile.MaxInstanceCount, defaultInstances)
                    .Parent()
                    .ApplyAsync();
            }

            return azure;
        }

        public static async Task<IAutoscaleSetting> ValidateAccessAsync(this IAutoscaleSettings autoscaleSettings, string resourceId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await autoscaleSettings.GetByIdAsync(resourceId, cancellationToken);
            }
            catch (ErrorResponseException ex)
            {
                var message = ex.Response != null & ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"The autoscale settings {resourceId} has not been found"
                    : $"Failed to access autoscale settings {resourceId}: {ex.Message}";
                throw new Exception(message);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to access autoscale settings {resourceId}: {ex.Message}");
            }
        }

        public static async Task<string> GetAccountConnectionString(this ICosmosDBAccounts accounts, string accountName, CancellationToken cancellationToken = default)
        {
            var cosmosAccounts = await accounts.ListAsync(cancellationToken: cancellationToken);
            var account = cosmosAccounts.FirstOrDefault(_ => _.Name == accountName);
            if (account == null)
            {
                throw new Exception($"The {accountName} cosmos account has not been found.");
            }

            var keys = await account.ListKeysAsync(cancellationToken);  // TODO: use ListConnectionString instead when it is fixed

            return $"AccountEndpoint={account.DocumentEndpoint};AccountKey={keys.PrimaryMasterKey};";
        }
    }

    // TODO: remove 
    public class UpdateAutoscaleProfileParameters
    {
        public string AutoscaleSettingsResourceId { get; set; }

        public string ProfileName { get; set; }

        public Func<IAutoscaleProfile, (int minInstance, int defaultInstances)> NewInstanceCountCalculator { get; set; }

        public bool ForceUpdate { get; set; }
    }
}
