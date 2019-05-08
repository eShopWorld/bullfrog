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

    public partial class ScaleEvent
    {
        /// <summary>
        /// Initializes a new instance of the ScaleEvent class.
        /// </summary>
        public ScaleEvent()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the ScaleEvent class.
        /// </summary>
        public ScaleEvent(string name, IList<RegionScaleValue> regionConfig, System.DateTimeOffset? requiredScaleAt = default(System.DateTimeOffset?), System.DateTimeOffset? startScaleDownAt = default(System.DateTimeOffset?))
        {
            Name = name;
            RequiredScaleAt = requiredScaleAt;
            StartScaleDownAt = startScaleDownAt;
            RegionConfig = regionConfig;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "requiredScaleAt")]
        public System.DateTimeOffset? RequiredScaleAt { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "startScaleDownAt")]
        public System.DateTimeOffset? StartScaleDownAt { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "regionConfig")]
        public IList<RegionScaleValue> RegionConfig { get; set; }

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
            if (RegionConfig == null)
            {
                throw new ValidationException(ValidationRules.CannotBeNull, "RegionConfig");
            }
            if (RegionConfig != null)
            {
                foreach (var element in RegionConfig)
                {
                    if (element != null)
                    {
                        element.Validate();
                    }
                }
            }
        }
    }
}