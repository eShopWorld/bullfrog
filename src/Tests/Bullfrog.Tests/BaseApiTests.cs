using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading;
using Bullfrog.Actors;
using Bullfrog.Actors.Helpers;
using Bullfrog.Common;
using Client;
using Eshopworld.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Rest;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;
using Moq;
using ServiceFabric.Mocks;

public class BaseApiTests
{
    public BaseApiTests()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(ConfigureServices)
            .UseStartup<TestServerStartup>();
        var server = new TestServer(builder);
        HttpClient = server.CreateClient();
        ApiClient = new BullfrogApi(new TokenCredentials("aa"), HttpClient, false);
    }

    protected HttpClient HttpClient { get; }

    protected BullfrogApi ApiClient { get; }

    protected ConfigurationManager ConfigurationManagerActor { get; private set; }

    protected Dictionary<(string scaleGroup, string region), ScaleManager> ScaleManagerActors { get; }
        = new Dictionary<(string scaleGroup, string region), ScaleManager>();

    protected DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2019, 2, 22, 0, 0, 0, 0,
       System.Globalization.CultureInfo.InvariantCulture.Calendar, TimeSpan.Zero);
    protected Mock<IScaleSetManager> ScaleSetManagerMoq { get; private set; }
    protected Mock<ICosmosManager> CosmosManagerMoq { get; private set; }

    private void ConfigureServices(IServiceCollection services)
    {
        var bigBrotherMoq = new Mock<IBigBrother>();
        services.AddSingleton(bigBrotherMoq.Object);

        services.AddTransient(_ => MockStatelessServiceContextFactory.Default);
        var actorProxyFactory = new MockActorProxyFactory();
        actorProxyFactory.MissingActor += ActoryProxyFactory_MissingActor;
        services.AddSingleton<IActorProxyFactory>(actorProxyFactory);

        var dateTimeProviderMoq = new Mock<IDateTimeProvider>();
        dateTimeProviderMoq.SetupGet(o => o.UtcNow).Returns(() => UtcNow);

        RegisterConfigurationManagerActor(actorProxyFactory);
        
        ScaleSetManagerMoq = new Mock<IScaleSetManager>();
        CosmosManagerMoq = new Mock<ICosmosManager>();
        RegisterScaleManagerActor("sg", "eu", ScaleSetManagerMoq, CosmosManagerMoq, dateTimeProviderMoq.Object, bigBrotherMoq.Object, actorProxyFactory);

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
        services.AddTransient(_ => cosmosDbManagerMoq.Object);

        var configurationValues = new Dictionary<string, string>
        {
            ["Bullfrog:Cosmos:ac"] = "/cosmos",
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        services.AddSingleton(configuration);
    }

    private void RegisterScaleManagerActor(string scaleGroup, string region, Mock<IScaleSetManager> scaleSetManagerMoq, Mock<ICosmosManager> cosmosManagerMoq, IDateTimeProvider dateTimeProvider, IBigBrother bigBrother, MockActorProxyFactory actorProxyFactory)
    {
        ActorBase scaleManagerActorFactory(ActorService service, ActorId id)
            => new ScaleManager(service, id, scaleSetManagerMoq.Object, cosmosManagerMoq.Object, dateTimeProvider, bigBrother);
        var scaleManagerSvc = MockActorServiceFactory.CreateActorServiceForActor<ScaleManager>(scaleManagerActorFactory);
        var scaleManagerActor = scaleManagerSvc.Activate(new ActorId($"ScaleManager:{scaleGroup}/{region}"));
        scaleManagerActor.InvokeOnActivateAsync().GetAwaiter().GetResult();
        actorProxyFactory.RegisterActor(scaleManagerActor);
        ScaleManagerActors.Add((scaleGroup, region), scaleManagerActor);
    }

    private void RegisterConfigurationManagerActor(MockActorProxyFactory actoryProxyFactory)
    {
        var id = new ActorId("configuration");
        ActorBase actorFactory(ActorService service, ActorId actorId)
            => new ConfigurationManager(service, actorId, actoryProxyFactory);
        var svc = MockActorServiceFactory.CreateActorServiceForActor<ConfigurationManager>(actorFactory);
        ConfigurationManagerActor = svc.Activate(id);
        ConfigurationManagerActor.InvokeOnActivateAsync().GetAwaiter().GetResult();
        actoryProxyFactory.RegisterActor(ConfigurationManagerActor);
    }

    private void ActoryProxyFactory_MissingActor(object sender, MissingActorEventArgs e)
    {
        throw new NotImplementedException(e.Id.ToString());
    }
}
