using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bullfrog.Common.Cosmos;
using Bullfrog.Common.Models;

namespace Bullfrog.Common.DependencyInjection
{
    internal class CosmosControlPlaneAccessValidator : ICosmosAccessValidator<CosmosDbControlPlaneConnection>
    {
        private readonly Func<CosmosDbControlPlaneConnection, ControlPlaneCosmosThroughputClient> _clientFactory;

        public CosmosControlPlaneAccessValidator(Func<CosmosDbControlPlaneConnection, ControlPlaneCosmosThroughputClient> clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public async Task<ValidationResult> ConfirmAccess(CosmosDbControlPlaneConnection connection)
        {
            try
            {
                var throughputClient = _clientFactory(connection);
                await throughputClient.Get();
                return ValidationResult.Success;
            }
            catch (Exception ex)
            {
                return new ValidationResult($"Failed to connect: {ex.Message}", new[] { nameof(connection.AccountResurceId) });
            }
        }
    }
}
