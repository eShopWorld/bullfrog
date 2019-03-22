using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Bullfrog.Api.Helpers
{
    /// <summary>
    /// The development-only authentication middleware which allows to similate specified access.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class FakeAuthenticationMiddleware
    {
        private const string FakeClaimHeaderPrefix = "FakeClaim-";
        private readonly RequestDelegate _next;

        /// <summary>
        /// Creates an instance.
        /// </summary>
        /// <param name="next">The next request delegate.</param>
        public FakeAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        /// <summary>
        /// Invokes the request processing.
        /// </summary>
        /// <param name="context">The request context.</param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            if (!context.User.Identity.IsAuthenticated)
            {
                var claims = new List<Claim>();
                foreach (var header in context.Request.Headers)
                {
                    if (header.Key.StartsWith(FakeClaimHeaderPrefix))
                    {
                        claims.Add(new Claim(header.Key.Substring(FakeClaimHeaderPrefix.Length), header.Value.ToString()));
                    }
                }

                if(claims.Count == 0)
                {
                    claims.Add(new Claim("scope", "bullfrog.api.all"));
                }

                var identity = new ClaimsIdentity(new GenericIdentity("FakeUser", "FakeAuth"), claims);
                context.User = new ClaimsPrincipal(identity);
            }

            await _next(context);
        }
    }
}
