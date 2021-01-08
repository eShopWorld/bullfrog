using System.Diagnostics.CodeAnalysis;
using Eshopworld.DevOps.KeyVault;

namespace Bullfrog.Common
{
    [ExcludeFromCodeCoverage]
    public class ServiceBusSettings
    {
        [KeyVaultSecretName("cm--sb-connection--esw-eda")]
        public string ConnectionString { get; set; }
        public string SubscriptionId { get; set; }
    }
}
