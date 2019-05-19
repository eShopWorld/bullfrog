using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors;
using Bullfrog.Actors.Helpers;
using Bullfrog.Common;
using Client;
using Eshopworld.Core;
using Helpers;
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
    private Mock<IDateTimeProvider> DateTimeProviderMoq;
    protected Mock<IScaleSetManager> ScaleSetManagerMoq { get; private set; }
    protected Mock<ICosmosManager> CosmosManagerMoq { get; private set; }
    protected Mock<IScaleSetMonitor> ScaleSetMonitorMoq { get; private set; }
    protected Mock<IBigBrother> BigBrotherMoq { get; private set; }

    private void ConfigureServices(IServiceCollection services)
    {
        BigBrotherMoq = new Mock<IBigBrother>();
        services.AddSingleton(BigBrotherMoq.Object);

        services.AddTransient(_ => MockStatelessServiceContextFactory.Default);
        var actorProxyFactory = new MockActorProxyFactory();
        actorProxyFactory.MissingActor += ActoryProxyFactory_MissingActor;
        services.AddSingleton<IActorProxyFactory>(new BullfrogMockActorProxyFactory(actorProxyFactory));

        DateTimeProviderMoq = new Mock<IDateTimeProvider>();
        DateTimeProviderMoq.SetupGet(o => o.UtcNow).Returns(() => UtcNow);

        RegisterConfigurationManagerActor(actorProxyFactory);

        ScaleSetManagerMoq = new Mock<IScaleSetManager>();
        CosmosManagerMoq = new Mock<ICosmosManager>();
        ScaleSetMonitorMoq = new Mock<IScaleSetMonitor>();
        RegisterScaleManagerActor("sg", "eu", ScaleSetManagerMoq, CosmosManagerMoq, ScaleSetMonitorMoq, DateTimeProviderMoq.Object, BigBrotherMoq.Object, actorProxyFactory);
        RegisterScaleManagerActor("sg", "eu1", ScaleSetManagerMoq, CosmosManagerMoq, ScaleSetMonitorMoq, DateTimeProviderMoq.Object, BigBrotherMoq.Object, actorProxyFactory);
        RegisterScaleManagerActor("sg", "eu2", ScaleSetManagerMoq, CosmosManagerMoq, ScaleSetMonitorMoq, DateTimeProviderMoq.Object, BigBrotherMoq.Object, actorProxyFactory);
        RegisterScaleManagerActor("sg", "eu3", ScaleSetManagerMoq, CosmosManagerMoq, ScaleSetMonitorMoq, DateTimeProviderMoq.Object, BigBrotherMoq.Object, actorProxyFactory);
        RegisterScaleManagerActor("sg", "$cosmos", ScaleSetManagerMoq, CosmosManagerMoq, ScaleSetMonitorMoq, DateTimeProviderMoq.Object, BigBrotherMoq.Object, actorProxyFactory);

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

        var authenticatedMoq = new Mock<Azure.IAuthenticated>();
        authenticatedMoq.Setup(x => x.WithSubscription("00000000-0000-0000-0000-000000000001"))
            .Returns(azureMoq.Object);
        services.AddSingleton(authenticatedMoq.Object);

        azureMoq.Setup(x => x.MetricDefinitions.ListByResourceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Mock<IReadOnlyList<IMetricDefinition>>().Object);

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

    private void RegisterScaleManagerActor(string scaleGroup, string region, Mock<IScaleSetManager> scaleSetManagerMoq, Mock<ICosmosManager> cosmosManagerMoq, Mock<IScaleSetMonitor> scaleSetMonitor, IDateTimeProvider dateTimeProvider, IBigBrother bigBrother, MockActorProxyFactory actorProxyFactory)
    {
        ActorBase scaleManagerActorFactory(ActorService service, ActorId id)
            => new ScaleManager(service, id, scaleSetManagerMoq.Object, cosmosManagerMoq.Object, scaleSetMonitor.Object, dateTimeProvider, actorProxyFactory, bigBrother);
        var stateProvider = new MyActorStateProvider(DateTimeProviderMoq.Object);
        var scaleManagerSvc = MockActorServiceFactory.CreateActorServiceForActor<ScaleManager>(scaleManagerActorFactory, stateProvider);
        var scaleManagerActor = scaleManagerSvc.Activate(new ActorId($"ScaleManager:{scaleGroup}/{region}"));
        scaleManagerActor.InvokeOnActivateAsync().GetAwaiter().GetResult();
        actorProxyFactory.RegisterActor(scaleManagerActor);
        ScaleManagerActors.Add((scaleGroup, region), scaleManagerActor);
    }

    private void RegisterConfigurationManagerActor(MockActorProxyFactory actoryProxyFactory)
    {
        var id = new ActorId("configuration");
        ActorBase actorFactory(ActorService service, ActorId actorId)
            => new ConfigurationManager(service, actorId, DateTimeProviderMoq.Object, actoryProxyFactory, BigBrotherMoq.Object);
        var stateProvider = new MyActorStateProvider(DateTimeProviderMoq.Object);
        var svc = MockActorServiceFactory.CreateActorServiceForActor<ConfigurationManager>(actorFactory, stateProvider);
        ConfigurationManagerActor = svc.Activate(id);
        ConfigurationManagerActor.InvokeOnActivateAsync().GetAwaiter().GetResult();
        actoryProxyFactory.RegisterActor(ConfigurationManagerActor);
    }

    private void ActoryProxyFactory_MissingActor(object sender, MissingActorEventArgs e)
    {
        throw new NotImplementedException(e.Id.ToString());
    }

    protected async Task AdvanceTimeTo(DateTimeOffset newTime)
    {
        while (true)
        {
            var reminders = ScaleManagerActors
                .SelectMany(a => a.Value.GetActorReminders())
                .Cast<MyActorReminderState>()
                .ToList();

            if (reminders.Count == 0)
                break;

            var nextExecution = reminders.Min(r => r.NextExecution);
            if (newTime < nextExecution)
                break;

            if (UtcNow <= nextExecution)
            {
                UtcNow = nextExecution;

                await RunSingleReminder(nextExecution);
            }
        }

        UtcNow = newTime;
    }

    private async Task RunSingleReminder(DateTimeOffset nextExecution)
    {
        foreach (var actor in ScaleManagerActors.Values)
        {
            var reminders = actor.GetActorReminders()
                .Cast<MyActorReminderState>();
            var reminder = reminders.FirstOrDefault(r => r.NextExecution == nextExecution);
            if (reminder != null)
            {
                await ((IRemindable)actor).ReceiveReminderAsync(reminder.Name, reminder.State, reminder.DueTime, reminder.Period);
                return;
            }
        }
    }
}
