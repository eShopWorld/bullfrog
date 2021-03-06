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
    /// Defines the scale requirements for the specified region.
    /// </summary>
    public partial class RegionScaleValue
    {
        /// <summary>
        /// Initializes a new instance of the RegionScaleValue class.
        /// </summary>
        public RegionScaleValue()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the RegionScaleValue class.
        /// </summary>
        /// <param name="name">The region name.</param>
        /// <param name="scale">The number of requests that the region should
        /// be able to handle during the scale event.</param>
        public RegionScaleValue(string name, int? scale = default(int?))
        {
            Name = name;
            Scale = scale;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// Gets or sets the region name.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the number of requests that the region should be able
        /// to handle during the scale event.
        /// </summary>
        [JsonProperty(PropertyName = "scale")]
        public int? Scale { get; set; }

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
            if (Scale > 1000000)
            {
                throw new ValidationException(ValidationRules.InclusiveMaximum, "Scale", 1000000);
            }
            if (Scale < 1)
            {
                throw new ValidationException(ValidationRules.InclusiveMinimum, "Scale", 1);
            }
        }
    }
}
