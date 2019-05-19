using System;
using System.Text.RegularExpressions;
using Microsoft.Azure.Management.Fluent;

namespace Bullfrog.Common
{
    /// <summary>
    /// Azure.IAuthenticated extension methods.
    /// </summary>
    public static class IAuthenticatedExtensions
    {
        private static readonly Regex ResourceSubscriptionPattern = new Regex(
            "^/subscriptions/([a-f0-9-]+)/", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>
        /// Returns the <see cref="IAzure"/> interface with a subscription owning given resource.
        /// </summary>
        /// <param name="authenticated">The <see cref="Azure.IAuthenticated"/> instance.</param>
        /// <param name="resourceId">The resource id.</param>
        /// <returns>Returns the subscription of the specified resource.</returns>
        public static IAzure WithSubscriptionFor(this Azure.IAuthenticated authenticated, string resourceId)
        {
            var match = ResourceSubscriptionPattern.Match(resourceId);
            if (!match.Success)
            {
                throw new ArgumentException($"The resource id '{resourceId}' is invalid.");
            }

            return authenticated.WithSubscription(match.Groups[1].Value);
        }
    }
}
