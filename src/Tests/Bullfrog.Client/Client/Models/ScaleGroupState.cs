// <auto-generated>
// Code generated by Microsoft (R) AutoRest Code Generator.
// Changes may cause incorrect behavior and will be lost if the code is
// regenerated.
// </auto-generated>

namespace Client.Models
{
    using Newtonsoft.Json;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public partial class ScaleGroupState
    {
        /// <summary>
        /// Initializes a new instance of the ScaleGroupState class.
        /// </summary>
        public ScaleGroupState()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the ScaleGroupState class.
        /// </summary>
        public ScaleGroupState(IList<ScaleRegionState> regions = default(IList<ScaleRegionState>))
        {
            Regions = regions;
            CustomInit();
        }

        /// <summary>
        /// An initialization method that performs custom operations like setting defaults
        /// </summary>
        partial void CustomInit();

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "regions")]
        public IList<ScaleRegionState> Regions { get; set; }

    }
}