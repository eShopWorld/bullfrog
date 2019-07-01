using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bullfrog.Actors.Interfaces.Models.Validation;
using Bullfrog.Common;
using Bullfrog.Common.Models;

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
        /// The Cosmos DB account name.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// The Cosmos DB database name.
        /// </summary>
        public string DatabaseName { get; set; }

        /// <summary>
        /// The optional name of the container in the Cosmos DB database.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// The data plane connection details.
        /// </summary>
        public CosmosDbDataPlaneConnection DataPlaneConnection { get; set; }

        /// <summary>
        /// The control plane connection details.
        /// </summary>
        public CosmosDbControlPlaneConnection ControlPlaneConnection { get; set; }

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
            if(DataPlaneConnection == null &&  ControlPlaneConnection == null)
            {
                // TODO: remove this after updating tests
                DataPlaneConnection = new CosmosDbDataPlaneConnection
                {
                    AccountName = AccountName,
                    ContainerName = ContainerName,
                    DatabaseName = DatabaseName,
                };
            }

            if (DataPlaneConnection != null)
            {
                if (ControlPlaneConnection != null)
                {
                    yield return new ValidationResult("Both data plane and control plane connection types must not be specified",
                        new[] { nameof(ControlPlaneConnection) });
                }

                yield return Validate(DataPlaneConnection, validationContext);
            }
            else if (ControlPlaneConnection != null)
            {

                yield return Validate(ControlPlaneConnection, validationContext);
            }
            else
            {
                yield return new ValidationResult("Either data plane or control plane connection type may be specified",
                    new[] { nameof(ControlPlaneConnection) });
            }
        }

        private static ValidationResult Validate<T>(T connection, ValidationContext validationContext)
        {
            var cosmosDbManager = (ICosmosAccessValidator<T>)
                  validationContext.GetService(typeof(ICosmosAccessValidator<T>));

            return cosmosDbManager.ConfirmAccess(connection).GetAwaiter().GetResult();
        }

        #endregion
    }
}
