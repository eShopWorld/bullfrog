using Bullfrog.Api;
using Bullfrog.Api.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

public class TestServerStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Bullfrog Api", Version = "v1" });
            c.OperationFilter<OperationIdFilter>();
        });

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
        app.UseRouting();
        app.UseFakeAuthentication();      
        app.UseEndpoints(endpoints => {
            endpoints.MapControllers();
        });
        app.UseSwagger(c => c.SerializeAsV2 = true);
        app.UseSwaggerUI();
    }
}
