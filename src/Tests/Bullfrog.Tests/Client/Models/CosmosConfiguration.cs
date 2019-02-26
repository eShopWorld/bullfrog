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

    /// <summary>
    /// Defines a Cosmos DB database or a container which is should be scaled
    /// to handle requested throughput.
    /// </summary>
    public partial class CosmosConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the CosmosConfiguration class.
        /// </summary>
        public CosmosConfiguration()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the CosmosConfiguration class.
        /// </summary>
        /// <param name="name">The name used as an identifier of this Cosmos DB
        /// instance.</param>
        /// <param name="accountName">The Cosmos DB account name.</param>
        /// <param name="databaseName">The Cosmos DB database name.</param>
        /// <param name="containerName">The optional name of the container in
        /// the Cosmos DB database.</param>
        /// <param name="requestUnitsPerRequest">The number of Request Units
        /// used on average by each request.</param>
        /// <param name="minimumRU">The minimal value of RU used when there are
        /// no active events.</param>
        /// <param name="maximumRU">The maximal value of RU. No scaling
        /// operation will exceed it.</param>
        public CosmosConfiguration(string name, string accountName, string databaseName, string containerName = default(string), double? requestUnitsPerRequest = default(double?), int? minimumRU = default(int?), int? maximumRU = default(int?))
        {
            Name = name;
            AccountName = accountName;
            DatabaseName = databaseName;
            ContainerName = containerName;
            RequestUnitsPerRequest = requestUnitsPerRequest;
            MinimumRU = minimumRU;
            MaximumRU = maximumRU;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Gets or sets the name used as an identifier of this Cosmos DB
        /// instance.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Cosmos DB account name.
        /// </summary>
        [JsonProperty(PropertyName = "accountName")]
        public string AccountName { get; set; }

        /// <summary>
        /// Gets or sets the Cosmos DB database name.
        /// </summary>
        [JsonProperty(PropertyName = "databaseName")]
        public string DatabaseName { get; set; }

        /// <summary>
        /// Gets or sets the optional name of the container in the Cosmos DB
        /// database.
        /// </summary>
        [JsonProperty(PropertyName = "containerName")]
        public string ContainerName { get; set; }

        /// <summary>
        /// Gets or sets the number of Request Units used on average by each
        /// request.
        /// </summary>
        [JsonProperty(PropertyName = "requestUnitsPerRequest")]
        public double? RequestUnitsPerRequest { get; set; }

        /// <summary>
        /// Gets or sets the minimal value of RU used when there are no active
        /// events.
        /// </summary>
        [JsonProperty(PropertyName = "minimumRU")]
        public int? MinimumRU { get; set; }

        /// <summary>
        /// Gets or sets the maximal value of RU. No scaling operation will
        /// exceed it.
        /// </summary>
        [JsonProperty(PropertyName = "maximumRU")]
        public int? MaximumRU { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            if (Name == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "Name");
            }
            if (AccountName == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "AccountName");
            }
            if (DatabaseName == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "DatabaseName");
            }
        }
    }
}
