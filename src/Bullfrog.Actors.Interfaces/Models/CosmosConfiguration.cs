﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bullfrog.Actors.Interfaces.Models.Validation;
using Bullfrog.Common;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// Defines a Cosmos DB database or a container which is should be scaled to handle requested throughput. 
    /// </summary>
    public class CosmosConfiguration : IValidatableObject
    {
        /// <summary>
        /// The name used as an identifier of this Cosmos DB instance.
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// The Cosmos DB account name when the data plane is used to control throughput.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// The Cosmos DB database name.
        /// </summary>
        [Required]
        public string DatabaseName { get; set; }

        /// <summary>
        /// The optional name of the container in the Cosmos DB database.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// The number of Request Units used on average by each request.
        /// </summary>
        [ValueIs(ValueComparison.GreaterThen, Value = 0)]
        public decimal RequestUnitsPerRequest { get; set; }

        /// <summary>
        /// The minimal value of RU used when there are no active events.
        /// </summary>
        [CosmosRU]
        public int MinimumRU { get; set; }

        /// <summary>
        /// The maximal value of RU. No scaling operation will exceed it.
        /// </summary>
        [CosmosRU]
        [ValueIs(ValueComparison.GreaterThanOrEqualTo, PropertyValue = nameof(MinimumRU))]
        public int MaximumRU { get; set; }

        #region Validation

        IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            var cosmosDbManager = (ICosmosDbHelper)validationContext.GetService(typeof(ICosmosDbHelper));

            yield return cosmosDbManager.ValidateConfiguration(new CosmosDbConfiguration
            {
                AccountName = AccountName,
                ContainerName = ContainerName,
                DatabaseName = DatabaseName,
            }).GetAwaiter().GetResult();
        }

        #endregion
    }
}
