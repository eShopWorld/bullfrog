using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors;
using Bullfrog.Actors.Helpers;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Actors.ResourceScalers;
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
    protected readonly DateTimeOffset StartTime = new DateTimeOffset(2019, 2, 22, 0, 0, 0, 0,
       System.Globalization.CultureInfo.InvariantCulture.Calendar, TimeSpan.Zero);

    protected HttpClient HttpClient { get; }

    protected BullfrogApi ApiClient { get; }

    protected ConfigurationManager ConfigurationManagerActor { get; private set; }

    protected Dictionary<(string scaleGroup, string region), ScaleManager> ScaleManagerActors { get; }
        = new Dictionary<(string scaleGroup, string region), ScaleManager>();

    protected DateTimeOffset UtcNow { get; set; }
    protected TimeSpan TimeSincStart => UtcNow - StartTime;

    private Mock<IDateTimeProvider> DateTimeProviderMoq;
    protected Mock<IResourceScalerFactory> ScalerFactoryMoq { get; private set; }
    protected Mock<IScaleSetMonitor> ScaleSetMonitorMoq { get; private set; }

    protected List<(DateTimeOffset Time, object Event)> NonTelemetryEvents { get; } = new List<(DateTimeOffset Time, object Event)>();
    protected List<(DateTimeOffset Time, TelemetryEvent Event)> BigBrotherEvents { get; } = new List<(DateTimeOffset Time, TelemetryEvent Event)>();

    protected readonly Dictionary<string, List<(TimeSpan SinceStart, int? RequestedThroughput, int? ReachedThroughput)>> ScaleHistory =
        new Dictionary<string, List<(TimeSpan SinceStart, int? RequestedThroughput, int? ReachedThroughput)>>();

    protected static string GetLoadBalancerResourceId()
        => "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg/providers/Microsoft.Network/loadBalancers/lb";

    protected static string GetAutoscaleSettingResourceId()
        => "/subscriptions/00000000-0000-0000-0000-000000000001/resourceGroups/rg/providers/microsoft.insights/autoscalesettings/as";
    public BaseApiTests()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(ConfigureServices)
            .UseStartup<TestServerStartup>();
        var server = new TestServer(builder);
        HttpClient = server.CreateClient();
        ApiClient = new BullfrogApi(new TokenCredentials("aa"), HttpClient, false);
        UtcNow = StartTime;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var bigBrother = new BigBrotherLogger(e => BigBrotherEvents.Add((UtcNow, e)), n => NonTelemetryEvents.Add((UtcNow, n)));
        services.AddSingleton<IBigBrother>(bigBrother);

        services.AddTransient(_ => MockStatelessServiceContextFactory.Default);
        var actorProxyFactory = new MockActorProxyFactory();
        actorProxyFactory.MissingActor += ActoryProxyFactory_MissingActor;
        services.AddSingleton<IActorProxyFactory>(new BullfrogMockActorProxyFactory(actorProxyFactory));

        DateTimeProviderMoq = new Mock<IDateTimeProvider>();
        DateTimeProviderMoq.SetupGet(o => o.UtcNow).Returns(() => UtcNow);

        RegisterConfigurationManagerActor(actorProxyFactory, bigBrother);

        ScalerFactoryMoq = new Mock<IResourceScalerFactory>(MockBehavior.Strict);
        ScaleSetMonitorMoq = new Mock<IScaleSetMonitor>();
        foreach (var regionName in "eu,eu1,eu2,eu3,$cosmos".Split(','))
        {
            RegisterScaleManagerActor("sg", regionName, ScalerFactoryMoq, ScaleSetMonitorMoq, DateTimeProviderMoq.Object, bigBrother, actorProxyFactory);
        }

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

    private void RegisterScaleManagerActor(string scaleGroup, string region, Mock<IResourceScalerFactory> scalerFactoryMoq, Mock<IScaleSetMonitor> scaleSetMonitor, IDateTimeProvider dateTimeProvider, IBigBrother bigBrother, MockActorProxyFactory actorProxyFactory)
    {
        ActorBase scaleManagerActorFactory(ActorService service, ActorId id)
            => new ScaleManager(service, id, scalerFactoryMoq.Object, scaleSetMonitor.Object, dateTimeProvider, actorProxyFactory, bigBrother);
        var stateProvider = new MyActorStateProvider(DateTimeProviderMoq.Object);
        var scaleManagerSvc = MockActorServiceFactory.CreateActorServiceForActor<ScaleManager>(scaleManagerActorFactory, stateProvider);
        var scaleManagerActor = scaleManagerSvc.Activate(new ActorId($"ScaleManager:{scaleGroup}/{region}"));
        scaleManagerActor.InvokeOnActivateAsync().GetAwaiter().GetResult();
        actorProxyFactory.RegisterActor(scaleManagerActor);
        ScaleManagerActors.Add((scaleGroup, region), scaleManagerActor);
    }

    private void RegisterConfigurationManagerActor(MockActorProxyFactory actoryProxyFactory, IBigBrother bigBrother)
    {
        var id = new ActorId("configuration");
        ActorBase actorFactory(ActorService service, ActorId actorId)
            => new ConfigurationManager(service, actorId, DateTimeProviderMoq.Object, actoryProxyFactory, bigBrother);
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

    private static long Round(long value, long multiple, long offset = 0)
    {
        return (value - offset - 1 + multiple) / multiple * multiple + offset;
    }

    protected IEnumerable<(DateTimeOffset Time, T Event)> GetPublishedEvents<T>()
        where T : TelemetryEvent
        => from e in BigBrotherEvents
           where e.Event is T
           select (e.Time, (T)e.Event);

    protected DateTimeOffset RoundToScan(DateTimeOffset dateTime)
    {
        return new DateTimeOffset(Round(dateTime.Ticks, ScaleManager.ScanPeriod.Ticks, StartTime.Ticks), dateTime.Offset);
    }

    protected void RegisterDefaultScalers()
    {
        var scalerMoq = new Mock<ResourceScaler>();
        scalerMoq.Setup(x => x.SetThroughput(It.IsAny<int?>()))
            .Returns((int? n) => Task.FromResult((int?)null));
        ScalerFactoryMoq.Setup(x => x.CreateScaler(It.IsAny<string>(), It.IsAny<ScaleManagerConfiguration>()))
            .Returns(scalerMoq.Object);
    }

    protected void RegisterResourceScaler(string name, Func<int?, int?> scaler)
    {
        int? CalculateAndSaveThroughput(int? requestedThroughput)
        {
            var reachedThroughput = scaler(requestedThroughput);
            ScaleHistory[name].Add((TimeSincStart, requestedThroughput, reachedThroughput));
            return reachedThroughput;
        }

        var scalerMoq = new Mock<ResourceScaler>();
        scalerMoq.Setup(x => x.SetThroughput(It.IsAny<int?>()))
            .Returns((int? n) => Task.FromResult(CalculateAndSaveThroughput(n)));
        ScalerFactoryMoq.Setup(x => x.CreateScaler(name, It.IsAny<ScaleManagerConfiguration>()))
            .Returns(scalerMoq.Object);
        ScaleHistory[name] = new List<(TimeSpan SinceStart, int? RequestedThroughput, int? ReachedThroughput)>();
    }

    protected void RegisterResourceScaler(string name, ResourceScaler scaler)
    {
        var logger = new ScalerLogger(scaler,
            (requestedThroughput, reachedThroughput) => ScaleHistory[name].Add((TimeSincStart, requestedThroughput, reachedThroughput)));
        ScalerFactoryMoq.Setup(x => x.CreateScaler(name, It.IsAny<ScaleManagerConfiguration>()))
            .Returns(logger);
        ScaleHistory[name] = new List<(TimeSpan SinceStart, int? RequestedThroughput, int? ReachedThroughput)>();
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

    private sealed class ScalerLogger : ResourceScaler
    {
        private readonly ResourceScaler _scaler;
        private readonly Action<int?, int?> _logSetThroughputAction;

        public ScalerLogger(ResourceScaler scaler, Action<int?, int?> logSetThroughputAction)
        {
            _scaler = scaler;
            _logSetThroughputAction = logSetThroughputAction;
        }

        public override async Task<int?> SetThroughput(int? newThroughput)
        {
            var result = await _scaler.SetThroughput(newThroughput);
            _logSetThroughputAction(newThroughput, result);
            return result;
        }
    }

    private class BigBrotherLogger : IBigBrother
    {
        private readonly Action<TelemetryEvent> _telemetryEventLogger;
        private readonly Action<object> _nontelemetryEventLogger;

        public BigBrotherLogger(Action<TelemetryEvent> telemetryEventLogger, Action<object> nontelemetryEventLogger)
        {
            _telemetryEventLogger = telemetryEventLogger;
            _nontelemetryEventLogger = nontelemetryEventLogger;
        }

        public IBigBrother DeveloperMode()
        {
            throw new NotImplementedException();
        }

        public void Flush()
        {
            throw new NotImplementedException();
        }

        public void Publish<T>(T @event, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1) where T : TelemetryEvent
        {
            _telemetryEventLogger(@event);
        }

        public void Publish(object @event, [CallerMemberName] string callerMemberName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            _nontelemetryEventLogger(@event);
        }

        public IBigBrother UseKusto(string kustoEngineName, string kustoEngineLocation, string kustoDb, string tenantId)
        {
            throw new NotImplementedException();
        }
    }
}


