using System;
using System.Threading.Tasks;

namespace Bullfrog.Actors.Helpers
{
    /// <summary>
    /// Creates an autoscale settings handlers.
    /// </summary>
    public interface IAutoscaleSettingsHandlerFactory
    {
        /// <summary>
        /// Creates an autoscale settings handler for the specified recource.
        /// </summary>
        /// <param name="autoscaleSettingsResourceId">The autoscale settings' resource id.</param>
        /// <param name="defaultProfileName">The name of the default profile.</param>
        /// <returns>Returns the handler.</returns>
        IAutoscaleSettingsHandler CreateHandler(string autoscaleSettingsResourceId, string defaultProfileName);
    }

    /// <summary>
    /// Reads and modifies autoscale settings.
    /// </summary>
    public interface IAutoscaleSettingsHandler
    {
        /// <summary>
        /// Reads the autoscale settings.
        /// </summary>
        /// <returns></returns>
        Task<AutoscaleSettingsSummary> Read();

        /// <summary>
        /// Removes Bullfrog's profile
        /// </summary>
        /// <returns>Returns new settings and a flag stating whether the resource has been modified.</returns>
        Task<(AutoscaleSettingsSummary autoscaleSettings, bool changed)> RemoveBullfrogProfile();

        /// <summary>
        /// Creates or updates Bullfrog's profile
        /// </summary>
        /// <param name="bullfrogProfile">The new values of the Bullfrog's profile.</param>
        /// <returns>Returns new settings and a flag stating whether the resource has been modified</returns>
        Task<(AutoscaleSettingsSummary autoscaleSettings, bool changed)> UpdateBullfrogProfile(BullfrogChange bullfrogProfile);
    }

    /// <summary>
    /// The autoscale settings' features used by scalers.
    /// </summary>
    public class AutoscaleSettingsSummary
    {
        /// <summary>
        /// The minimum number of instances defined in the default profile.
        /// </summary>
        public int DefaultMinimum { get; set; }

        /// <summary>
        /// The maximum number of instances defined in the default profile.
        /// </summary>
        public int DefaultMaximum { get; set; }

        /// <summary>
        /// The optional details about Bullfrog's profile.
        /// </summary>
        public BullfrogProfileSummary BullfrogProfile { get; set; }
    }

    /// <summary>
    /// Represents Bullfrog's profile.
    /// </summary>
    public class BullfrogProfileSummary
    {
        /// <summary>
        /// The minimum number of instances.
        /// </summary>
        public int Minimum { get; set; }

        /// <summary>
        /// The maximum number of instances.
        /// </summary>
        public int Maximum { get; set; }

        /// <summary>
        /// The time when Bullfrog's profile is activated.
        /// </summary>
        public DateTimeOffset Starts { get; set; }

        /// <summary>
        /// The time when Bullfrog's profile is deactivated.
        /// </summary>
        public DateTimeOffset Ends { get; set; }
    }

    /// <summary>
    /// The new parameters of Bullfrog's profile.
    /// </summary>
    public class BullfrogChange
    {
        /// <summary>
        /// The minimum number of instances.
        /// </summary>
        public int Minimum { get; set; }

        /// <summary>
        /// The time when Bullfrog's profile is activated.
        /// </summary>
        public DateTimeOffset Starts { get; set; }

        /// <summary>
        /// The time when Bullfrog's profile is deactivated. (must be not earlier than <see cref="Starts"/>)
        /// </summary>
        public DateTimeOffset Ends { get; set; }
    }
}
