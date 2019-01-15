using System;
using System.Diagnostics;
using System.Fabric;
using System.IO;
using System.Reflection;
using Autofac;
using Bullfrog.Api.Helpers;
using Bullfrog.Common.DependencyInjection;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Eshopworld.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace Bullfrog.Api
{
    /// <summary>
    /// Configures the application.
    /// </summary>
    public class Startup
    {
        // TODO: Review BB code after fixing its extension methods
        private readonly BigBrother _bb;
        private readonly IConfigurationRoot _configuration;

        /// <summary>
        /// Creates an instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="env">The hosting environment.</param>
        public Startup(IHostingEnvironment env)
        {
            try
            {
                _configuration = EswDevOpsSdk.BuildConfiguration(env.ContentRootPath, env.EnvironmentName);
                var internalKey = _configuration["BBInstrumentationKey"];
                _bb = new BigBrother(internalKey, internalKey);
                _bb.UseEventSourceSink().ForExceptions();
            }
            catch (Exception e)
            {
                BigBrother.Write(e);
                throw;
            }
        }

        /// <summary>
        /// The framework service configuration entry point.
        ///     Do not use this to setup anything without a <see cref="IServiceCollection"/> extension method!
        /// </summary>
        /// <param name="services">The contract for a collection of service descriptors.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            try
            {
                services.AddApplicationInsightsTelemetry(_configuration);
                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc(BullfrogVersion.LatestApi, new Info { Title = "Bullfrog Api", Version = BullfrogVersion.Bullfrog });
                    c.AddSecurityDefinition("Bearer",
                        new ApiKeyScheme
                        {
                            In = "header",
                            Description = "Please insert JWT with Bearer into field",
                            Name = "Authorization",
                            Type = "apiKey"
                        });
                    var filePath = Path.Combine(
                        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new InvalidOperationException("Wrong check for the swagger XML file! 'Assembly.GetExecutingAssembly().Location' came back null!"),
                        "Bullfrog.Api.xml");

                    if (File.Exists(filePath))
                    {
                        c.IncludeXmlComments(filePath);
                    }
                    else
                    {
                        if (Debugger.IsAttached)
                        {
                            // Couldn't find the XML file! check that XML comments are being built and that the file name checks
                            Debugger.Break();
                        }
                    }
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

                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddIdentityServerAuthentication(x =>
                {
                    x.ApiName = _configuration["STSConfig:ApiName"];
                    x.Authority = _configuration["STSConfig:Authority"];
                });

                services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            }
            catch (Exception e)
            {
                _bb.Publish(e.ToExceptionEvent());
                _bb.Flush();
                throw;
            }
        }

        /// <summary>
        /// The framework DI container configuration entry point.
        ///     Use this to setup specific AutoFac dependencies that don't have <see cref="IServiceCollection"/> extension methods.
        /// </summary>
        /// <param name="builder">The builder for an <see cref="T:Autofac.IContainer" /> from component registrations.</param>
        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterModule(new CoreModule());
        }

        /// <summary>
        /// The framework HTTP request pipeline configuration entry point.
        /// </summary>
        /// <param name="app">The mechanisms to configure an application's request pipeline.</param>
        /// <param name="env">The information about the web hosting environment an application is running in.</param>
        /// <param name="statelessServiceContext">The context of Service Fabric stateless service.</param>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, StatelessServiceContext statelessServiceContext)
        {
            try
            {
#if OAUTH_OFF_MODE
                app.UseFakeAuthentication();
#else
                app.UseAuthentication();
#endif
                if (Debugger.IsAttached)
                    app.UseDeveloperExceptionPage();
                app.UseBigBrotherExceptionHandler();
                if (_configuration.GetValue<bool>("ActorDirectCallMiddlewareEnabled"))
                {
                    app.UseActorDirectCall(new ActorDirectCallOptions
                    {
                        StatelessServiceContext = statelessServiceContext,
                    });
                }

                app.UseMvc();

                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint($"/swagger/{BullfrogVersion.LatestApi}/swagger.json", $"Bullfrog Api {BullfrogVersion.LatestApi}");
                });
            }
            catch (Exception e)
            {
                _bb.Publish(e.ToExceptionEvent());
                _bb.Flush();
                throw;
            }
        }
    }
}
