using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Fabric;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors;
using Bullfrog.Actors.Helpers;
using Bullfrog.Api;
using Bullfrog.Api.Controllers;
using Bullfrog.Api.Helpers;
using Bullfrog.Common;
using Eshopworld.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;
using Moq;
using ServiceFabric.Mocks;

public class TestServerStartup
{
    public int MyProperty { get; set; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc()
            .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        //.AddApplicationPart(typeof(ProbeController).Assembly);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthenticationPolicies.AdminScope, policy =>
                policy.RequireClaim("scope", "bullfrog.api.all"));
            options.AddPolicy(AuthenticationPolicies.EventsManagerScope, policy =>
                policy.RequireClaim("scope", "bullfrog.api.events.all", "bullfrog.api.all"));
            options.AddPolicy(AuthenticationPolicies.EventsReaderScope, policy =>
                policy.RequireClaim("scope", "bullfrog.api.events.read", "bullfrog.api.events.all", "bullfrog.api.all"));
        });

        var bigBrotherMoq = new Mock<IBigBrother>();
        services.AddSingleton(bigBrotherMoq.Object);

        services.AddTransient(_ => MockStatelessServiceContextFactory.Default);
        var actoryProxyFactory = new MockActorProxyFactory();
        actoryProxyFactory.MissingActor += ActoryProxyFactory_MissingActor;
        services.AddSingleton<IActorProxyFactory>(actoryProxyFactory);

        var id = new ActorId("configuration");
        Func<ActorService, ActorId, ActorBase> actorFactory = (service, actorId) => new ConfigurationManager(service, actorId, new SimpleActorProxyFactory(actoryProxyFactory));
        var svc = MockActorServiceFactory.CreateActorServiceForActor<ConfigurationManager>(actorFactory);
        var actor = svc.Activate(id);

        actoryProxyFactory.RegisterActor(actor);

        var scaleSetManagerMoq = new Mock<IScaleSetManager>();
        var cosmosManagerMoq = new Mock<ICosmosManager>();
        var regId = new ActorId("ScaleManager:sg/eu");
        Func<ActorService, ActorId, ActorBase> scaleManagerActorFactory = (service, actorId)
            => new ScaleManager(service, actorId, scaleSetManagerMoq.Object, cosmosManagerMoq.Object, bigBrotherMoq.Object);
        var scaleManagerSvc = MockActorServiceFactory.CreateActorServiceForActor<ScaleManager>(scaleManagerActorFactory);
        var scaleManagerActor = scaleManagerSvc.Activate(regId);
        scaleManagerActor.InvokeOnActivateAsync().GetAwaiter().GetResult();
        actoryProxyFactory.RegisterActor(scaleManagerActor);


        var autoscaleProfile = new Mock<IAutoscaleProfile>();
        autoscaleProfile.SetupGet(p => p.MaxInstanceCount).Returns(10);
        var profiles = new Dictionary<string, IAutoscaleProfile> { ["pr"] = autoscaleProfile.Object };
        var autoscaleSettingsMoq = new Mock<IAutoscaleSetting>();
        autoscaleSettingsMoq.SetupGet(x => x.Profiles)
            .Returns(profiles);
        var azureMoq = new Mock<IAzure>();
        azureMoq.Setup(x => x.AutoscaleSettings.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(autoscaleSettingsMoq.Object);
        autoscaleSettingsMoq.Setup(x => x.Update().UpdateAutoscaleProfile(It.IsAny<string>()).WithMetricBasedScale(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()).Parent().ApplyAsync(It.IsAny<CancellationToken>(), true))
            .ReturnsAsync(autoscaleSettingsMoq.Object);
        services.AddSingleton(azureMoq.Object);

        var cosmosDbManagerMoq = new Mock<ICosmosDbHelper>();
        cosmosDbManagerMoq.Setup(m => m.ValidateConfiguration(It.IsAny<CosmosDbConfiguration>()))
            .ReturnsAsync(ValidationResult.Success);
        services.AddTransient<ICosmosDbHelper>(_ => cosmosDbManagerMoq.Object);

        var configurationValues = new Dictionary<string, string>
        {
            ["Bullfrog:Cosmos:ac"] = "/cosmos",
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        services.AddSingleton<IConfigurationRoot>(configuration);
    }

    private void ActoryProxyFactory_MissingActor(object sender, MissingActorEventArgs e)
    {
        throw new NotImplementedException(e.Id.ToString());
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseFakeAuthentication();
        app.UseMvc();
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    private class SimpleActorProxyFactory : ISimpleActorProxyFactory
    {
        private readonly IActorProxyFactory _proxyFactory;

        public SimpleActorProxyFactory(IActorProxyFactory proxyFactory)
        {
            _proxyFactory = proxyFactory;
        }

        public TActor CreateProxy<TActor>(ActorId actorId) where TActor : IActor
        {
            return _proxyFactory.CreateActorProxy<TActor>(actorId);
        }
    }
}
