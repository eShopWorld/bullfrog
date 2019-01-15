using System;
using Microsoft.AspNetCore.Builder;

namespace Bullfrog.Api.Helpers
{
    /// <summary>
    /// Extension methods to add fake authentication capabilities to an HTTP application pipeline.
    /// </summary>
    public static class FakeAuthBuilderExtensions
    {
        /// <summary>
        /// Adds the <see cref="T:FakeAuthenticationMiddleware" /> to the specified <see cref="T:Microsoft.AspNetCore.Builder.IApplicationBuilder" />, which simulates authentication of users.
        /// </summary>
        /// <param name="app">The <see cref="T:Microsoft.AspNetCore.Builder.IApplicationBuilder" /> to add the middleware to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static  IApplicationBuilder UseFakeAuthentication(this IApplicationBuilder app)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            return app.UseMiddleware<FakeAuthenticationMiddleware>(Array.Empty<object>());
        }
    }
}
