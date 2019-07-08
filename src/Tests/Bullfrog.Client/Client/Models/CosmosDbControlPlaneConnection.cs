// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Client.Models
{
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using System.Linq;

    public partial class CosmosDbControlPlaneConnection
    {
        /// <summary>
        /// Initializes a new instance of the CosmosDbControlPlaneConnection
        /// class.
        /// </summary>
        public CosmosDbControlPlaneConnection()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the CosmosDbControlPlaneConnection
        /// class.
        /// </summary>
        public CosmosDbControlPlaneConnection(string accountResurceId, string databaseName, string containerName = default(string))
        {
            AccountResurceId = accountResurceId;
            DatabaseName = databaseName;
            ContainerName = containerName;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "accountResurceId")]
        public string AccountResurceId { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "databaseName")]
        public string DatabaseName { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "containerName")]
        public string ContainerName { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            if (AccountResurceId == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "AccountResurceId");
            }
            if (DatabaseName == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "DatabaseName");
            }
        }
    }
}
