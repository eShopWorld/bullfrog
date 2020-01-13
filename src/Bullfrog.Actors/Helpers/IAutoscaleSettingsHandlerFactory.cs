using System;
using System.Threading.Tasks;

namespace Bullfrog.Actors.Helpers
{
    /// <summary>
    /// Creates an autoscale settings handler for the specified resource.
    /// </summary>
    public interface IAutoscaleSettingsHandlerFactory
    {
        /// <summary>
        /// Creates an autoscale settings handler.
        /// </summary>
        /// <param name="autoscaleSettingsResourceId">The autoscale settings' resource id.</param>
        /// <param name="defaultProfileName">The name of the default profile.</param>
        /// <returns>Returns the handler.</returns>
        IAutoscaleSettingsHandler CreateHandler(string autoscaleSettingsResourceId, string defaultProfileName);
    }

    /// <summary>
    /// Enables reading and modifications of autoscale settings.
    /// </summary>
    public interface IAutoscaleSettingsHandler
    {
        /// <summary>
        /// Reads the autoscale settings.
        /// </summary>
        /// <returns></returns>
        Task<AutoscaleSettingsView> Read();

        /// <summary>
        /// Removes Bullfrog's profile
        /// </summary>
        /// <returns>Returns new settings and a flag whether modification were necessary</returns>
        Task<(AutoscaleSettingsView autoscaleSettings, bool chaged)> RemoveBullfrogProfile();

        /// <summary>
        /// Creates or updates Bullfrog's profile
        /// </summary>
        /// <param name="bullfrogProfile">The parameters of Bullfrog's profile.</param>
        /// <returns>Returns new settings and a flag whether modification were necessary</returns>
        Task<(AutoscaleSettingsView autoscaleSettings, bool chaged)> UpdateBullfrogProfile(BullfrogChange bullfrogProfile);
    }

    /// <summary>
    /// Represents the autoscale settings.
    /// </summary>
    public class AutoscaleSettingsView
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
        public BullfrogProfileView BullfrogProfile { get; set; }
    }

    /// <summary>
    /// Represents Bullfrog's profile.
    /// </summary>
    public class BullfrogProfileView
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
