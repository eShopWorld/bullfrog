using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;

namespace Bullfrog.Common
{
    /// <summary>
    /// Azure.IAuthenticated extension methods.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class IAuthenticatedExtensions
    {
        /// <summary>
        /// Returns the <see cref="IAzure"/> interface with a subscription owning given resource.
        /// </summary>
        /// <param name="authenticated">The <see cref="Azure.IAuthenticated"/> instance.</param>
        /// <param name="resourceId">The resource id.</param>
        /// <returns>Returns the subscription of the specified resource.</returns>
        public static IAzure WithSubscriptionFor(this Azure.IAuthenticated authenticated, string resourceId)
        {
            ResourceId rid;
            try
            {
                rid = ResourceId.FromString(resourceId);
                
            }
            catch (ArgumentException)
            {
                throw new ArgumentException($"The resource id '{resourceId}' is invalid.");
            }

            return authenticated.WithSubscription(rid.SubscriptionId);
        }
    }
}
