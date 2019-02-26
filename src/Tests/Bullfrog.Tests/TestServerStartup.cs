using Bullfrog.Api;
using Bullfrog.Api.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

public class TestServerStartup
{
    public int MyProperty { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc()
            .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthenticationPolicies.AdminScope, policy =>
                policy.RequireClaim("scope", "bullfrog.api.all"));
            options.AddPolicy(AuthenticationPolicies.EventsManagerScope, policy =>
                policy.RequireClaim("scope", "bullfrog.api.events.all", "bullfrog.api.all"));
            options.AddPolicy(AuthenticationPolicies.EventsReaderScope, policy =>
                policy.RequireClaim("scope", "bullfrog.api.events.read", "bullfrog.api.events.all", "bullfrog.api.all"));
        });

    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseFakeAuthentication();
        app.UseMvc();
        app.UseSwagger();
        app.UseSwaggerUI();
    }
}
