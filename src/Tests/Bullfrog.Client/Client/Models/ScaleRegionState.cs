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

    public partial class ScaleRegionState
    {
        /// <summary>
        /// Initializes a new instance of the ScaleRegionState class.
        /// </summary>
        public ScaleRegionState()
        {
            CustomInit();
        }

        /// <summary>
        /// Initializes a new instance of the ScaleRegionState class.
        /// </summary>
        public ScaleRegionState(string name = default(string), System.DateTimeOffset? wasScaledUpAt = default(System.DateTimeOffset?), System.DateTimeOffset? willScaleDownAt = default(System.DateTimeOffset?), double? scale = default(double?), int? requestedScale = default(int?), IDictionary<string, double?> scaleSetState = default(IDictionary<string, double?>))
        {
            Name = name;
            WasScaledUpAt = wasScaledUpAt;
            WillScaleDownAt = willScaleDownAt;
            Scale = scale;
            RequestedScale = requestedScale;
            ScaleSetState = scaleSetState;
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
        [JsonProperty(PropertyName = "wasScaledUpAt")]
        public System.DateTimeOffset? WasScaledUpAt { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "willScaleDownAt")]
        public System.DateTimeOffset? WillScaleDownAt { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "scale")]
        public double? Scale { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "requestedScale")]
        public int? RequestedScale { get; set; }

        /// <summary>
        /// </summary>
        [JsonProperty(PropertyName = "scaleSetState")]
        public IDictionary<string, double?> ScaleSetState { get; set; }

    }
}