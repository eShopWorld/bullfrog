// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Client.Models
{
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The scale group's region configuration.
    /// </summary>
    public partial class ScaleGroupRegion
    {
        /// <summary>
        /// Initializes a new instance of the ScaleGroupRegion class.
        /// </summary>
        public ScaleGroupRegion()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the ScaleGroupRegion class.
        /// </summary>
        /// <param name="regionName">The name of the region.</param>
        /// <param name="scaleSets">The configurations of scaling of VM scale
        /// sets.</param>
        /// <param name="cosmos">The configuration of scaling of Cosmos DB
        /// databases or containers.</param>
        /// <param name="scaleSetPrescaleLeadTime">VM scale sets prescale lead
        /// time.</param>
        /// <param name="cosmosDbPrescaleLeadTime">Cosmos DB prescale lead
        /// time.</param>
        public ScaleGroupRegion(string regionName, IList<ScaleSetConfiguration> scaleSets, IList<CosmosConfiguration> cosmos, ScaleSetConfiguration scaleSet = default(ScaleSetConfiguration), string scaleSetPrescaleLeadTime = default(string), string cosmosDbPrescaleLeadTime = default(string))
        {
            RegionName = regionName;
            ScaleSet = scaleSet;
            ScaleSets = scaleSets;
            Cosmos = cosmos;
            ScaleSetPrescaleLeadTime = scaleSetPrescaleLeadTime;
            CosmosDbPrescaleLeadTime = cosmosDbPrescaleLeadTime;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Gets or sets the name of the region.
        /// </summary>
        [JsonProperty(PropertyName = "regionName")]
        public string RegionName { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "scaleSet")]
        public ScaleSetConfiguration ScaleSet { get; set; }

        /// <summary>
        /// Gets or sets the configurations of scaling of VM scale sets.
        /// </summary>
        [JsonProperty(PropertyName = "scaleSets")]
        public IList<ScaleSetConfiguration> ScaleSets { get; set; }

        /// <summary>
        /// Gets or sets the configuration of scaling of Cosmos DB databases or
        /// containers.
        /// </summary>
        [JsonProperty(PropertyName = "cosmos")]
        public IList<CosmosConfiguration> Cosmos { get; set; }

        /// <summary>
        /// Gets or sets VM scale sets prescale lead time.
        /// </summary>
        [JsonProperty(PropertyName = "scaleSetPrescaleLeadTime")]
        public string ScaleSetPrescaleLeadTime { get; set; }

        /// <summary>
        /// Gets or sets cosmos DB prescale lead time.
        /// </summary>
        [JsonProperty(PropertyName = "cosmosDbPrescaleLeadTime")]
        public string CosmosDbPrescaleLeadTime { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            if (RegionName == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "RegionName");
            }
            if (ScaleSets == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "ScaleSets");
            }
            if (Cosmos == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "Cosmos");
            }
            if (ScaleSet != null)
            {
                ScaleSet.Validate();
            }
            if (ScaleSets != null)
            {
                foreach (var element in ScaleSets)
                {
                    if (element != null)
                    {
                        element.Validate();
                    }
                }
            }
            if (Cosmos != null)
            {
                foreach (var element1 in Cosmos)
                {
                    if (element1 != null)
                    {
                        element1.Validate();
                    }
                }
            }
        }
    }
}
