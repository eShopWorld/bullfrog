using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Bullfrog.Api.Helpers
{
    internal class FakeAuthenticationMiddleware
    {
        private const string FakeClaimHeaderPrefix = "FakeClaim-";
        private readonly RequestDelegate _next;

        public FakeAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

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
