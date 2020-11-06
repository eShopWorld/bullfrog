// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Client.Models
{
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public partial class ScaleGroupDefinition
    {
        /// <summary>
        /// Initializes a new instance of the ScaleGroupDefinition class.
        /// </summary>
        public ScaleGroupDefinition()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the ScaleGroupDefinition class.
        /// </summary>
        public ScaleGroupDefinition(IList<ScaleGroupRegion> regions, IList<CosmosConfiguration> cosmos = default(IList<CosmosConfiguration>), TimeSpan cosmosDbPrescaleLeadTime = default(TimeSpan), IList<AutomationAccount> automationAccounts = default(IList<AutomationAccount>), TimeSpan oldEventsAge = default(TimeSpan), bool? hasSharedCosmosDb = default(bool?), IList<string> allRegionNames = default(IList<string>))
        {
            Regions = regions;
            Cosmos = cosmos;
            CosmosDbPrescaleLeadTime = cosmosDbPrescaleLeadTime;
            AutomationAccounts = automationAccounts;
            OldEventsAge = oldEventsAge;
            HasSharedCosmosDb = hasSharedCosmosDb;
            AllRegionNames = allRegionNames;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "regions")]
        public IList<ScaleGroupRegion> Regions { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "cosmos")]
        public IList<CosmosConfiguration> Cosmos { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "cosmosDbPrescaleLeadTime")]
        public TimeSpan CosmosDbPrescaleLeadTime { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "automationAccounts")]
        public IList<AutomationAccount> AutomationAccounts { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "oldEventsAge")]
        public TimeSpan OldEventsAge { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "hasSharedCosmosDb")]
        public bool? HasSharedCosmosDb { get; private set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "allRegionNames")]
        public IList<string> AllRegionNames { get; private set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
            if (Regions == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "Regions");
            }
            if (Regions != null)
            {
                foreach (var element in Regions)
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
            if (AutomationAccounts != null)
            {
                foreach (var element2 in AutomationAccounts)
                {
                    if (element2 != null)
                    {
                        element2.Validate();
                    }
                }
            }
        }
    }
}
