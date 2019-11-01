using Microsoft.Azure.Management.AppService.Fluent.Models;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// Defines the ListScaleEvents parameters.
    /// </summary>
    public class ListScaleEventsParameters
    {
        /// <summary>
        /// Specifies whether only not completed events should be returned.
        /// </summary>
        public bool ActiveOnly { get; set; }

        /// <summary>
        /// Specifies optional region from which regions should be returned.
        /// </summary>
        public string FromRegion { get; set; }
    }
}
