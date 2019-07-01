using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bullfrog.Common.Cosmos;
using Bullfrog.Common.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Bullfrog.Common.DependencyInjection
{
    internal class CosmosControlPlaneAccessValidator : ICosmosAccessValidator<CosmosDbControlPlaneConnection>
    {
        private readonly IResourceManagementClient _resourceManagementClient;

        public CosmosControlPlaneAccessValidator(IResourceManagementClient resourceManagementClient)
        {
            _resourceManagementClient = resourceManagementClient;
        }

        public async Task<ValidationResult> ConfirmAccess(CosmosDbControlPlaneConnection connection)
        {
            try
            {
                await _resourceManagementClient.GetThroughput(connection);
                return ValidationResult.Success;
            }
            catch (Exception ex)
            {
                return new ValidationResult($"Failed to connect: {ex.Message}", new[] { nameof(connection.AccountResurceId) });
            }
        }
    }
}
