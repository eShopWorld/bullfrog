using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Fabric;
using System.IO;
using System.Linq;
using System.Reflection;
using Autofac;
using Bullfrog.Api.Helpers;
using Bullfrog.Common;
using Bullfrog.Common.DependencyInjection;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.ServiceFabric.DependencyInjection;
using Eshopworld.Telemetry;
using Eshopworld.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace Bullfrog.Api
{
    /// <summary>
    /// Configures the application.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class Startup
    {
        // TODO: Review BB code after fixing its extension methods
        private readonly BigBrother _bb;
        private readonly IConfigurationRoot _configuration;
        private bool UseOpenApiV2 => _configuration["Bullfrog:OpenApi"] == "V2";

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
                if (string.IsNullOrEmpty(internalKey))
                {
                    throw new BullfrogException($"BBIntrumentationKey not found for environment {env.EnvironmentName}");
                }

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
                var internalKey = _configuration["BBInstrumentationKey"];
                services.AddSingleton(new TelemetrySettings
                {
                    InstrumentationKey = internalKey,
                    InternalKey = internalKey,
                });
                services.AddApplicationInsightsTelemetry(internalKey);
                services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc(BullfrogVersion.LatestApi, new OpenApiInfo { Title = "Bullfrog Api", Version = BullfrogVersion.Bullfrog });
                    c.AddSecurityDefinition("Bearer",
                        new OpenApiSecurityScheme
                        {
                            In = ParameterLocation.Header,
                            Description = "Please insert JWT with Bearer into field",
                            Name = "Authorization",
                            Type = UseOpenApiV2 ? SecuritySchemeType.ApiKey : SecuritySchemeType.Http,
                            Scheme = "bearer",
                            BearerFormat = "JWT",
                        });
                    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                            },
                            Array.Empty<string>()
                        }
                    });

                    var docFiles = Directory.EnumerateFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Bullfrog.*.xml").ToList();
                    if (docFiles.Count > 0)
                    {
                        foreach (var file in docFiles)
                        {
                            c.IncludeXmlComments(file);
                        }
                    }
                    else
                    {
                        if (Debugger.IsAttached)
                        {
                            // Couldn't find the XML file! check that XML comments are being built and that the file name checks
                            Debugger.Break();
                        }
                    }

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
        /// <param name="builder">The builder for an <see cref="Autofac.IContainer" /> from component registrations.</param>
        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterModule<BullfrogCoreModule>();
            builder.RegisterModule<CoreModule>();
            builder.RegisterModule<AzureManagementFluentModule>();
            builder.RegisterModule<ServiceFabricModule>();
            builder.RegisterModule<ThroughputClientModule>();
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

                app.UseMvc();

                // Use V2 OpenAPI as long as AutoREST only supports V2.
                app.UseSwagger(c => c.SerializeAsV2 = UseOpenApiV2);
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
