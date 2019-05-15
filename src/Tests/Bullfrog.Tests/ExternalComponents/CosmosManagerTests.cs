using System.Threading.Tasks;
using Autofac;
using Bullfrog.Actors.Helpers;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

namespace ExternalComponents
{
    [Collection(ComponentsTestsCollection.TestFixtureName)]
    public class CosmosManagerTests
    {
        private const string TestDbName = "testdb";
        private readonly ComponentsTestFixture _fixture;

        public CosmosManagerTests(ComponentsTestFixture fixture)
        {
            _fixture = fixture;
            _fixture.BuildContainer();
        }

        [Fact, IsLayer1]
        public async Task SetScaleTest()
        {
            await CreateWithThroughput(400);

            var cosmosManager = _fixture.Container.Resolve<ICosmosManager>();
            var cosmosConfiguration = new Bullfrog.Actors.Interfaces.Models.CosmosConfiguration
            {
                Name = "test",
                AccountName = "bullfrog-cosmos-test",
                DatabaseName = TestDbName,
                MinimumRU = 400,
                MaximumRU = 600,
                RequestUnitsPerRequest = 20,
            };
            var rus = await cosmosManager.SetScale(24, cosmosConfiguration);

            rus.Should().Be(500);
            var newThoughput = await _fixture.GetCosmosClient().Databases[TestDbName].ReadProvisionedThroughputAsync();
            newThoughput.Should().Be(500);
        }

        [Fact, IsLayer1]
        public async Task MaxScaleIsObeyed()
        {
            await CreateWithThroughput(400);

            var cosmosManager = _fixture.Container.Resolve<ICosmosManager>();
            var cosmosConfiguration = new Bullfrog.Actors.Interfaces.Models.CosmosConfiguration
            {
                Name = "test",
                AccountName = "bullfrog-cosmos-test",
                DatabaseName = TestDbName,
                MinimumRU = 400,
                MaximumRU = 600,
                RequestUnitsPerRequest = 20,
            };
            var rus = await cosmosManager.SetScale(50, cosmosConfiguration);

            rus.Should().Be(600);
            var newThoughput = await _fixture.GetCosmosClient().Databases[TestDbName].ReadProvisionedThroughputAsync();
            newThoughput.Should().Be(600);
        }

        [Fact, IsLayer1]
        public async Task MinScaleIsObeyed()
        {
            await CreateWithThroughput(500);

            var cosmosManager = _fixture.Container.Resolve<ICosmosManager>();
            var cosmosConfiguration = new Bullfrog.Actors.Interfaces.Models.CosmosConfiguration
            {
                Name = "test",
                AccountName = "bullfrog-cosmos-test",
                DatabaseName = TestDbName,
                MinimumRU = 400,
                MaximumRU = 600,
                RequestUnitsPerRequest = 20,
            };
            var rus = await cosmosManager.SetScale(10, cosmosConfiguration);

            rus.Should().Be(400);
            var newThoughput = await _fixture.GetCosmosClient().Databases[TestDbName].ReadProvisionedThroughputAsync();
            newThoughput.Should().Be(400);
        }

        [Fact, IsLayer1]
        public async Task MinThroughputIsObeyed()
        {
            await CreateWithThroughput(500);

            var cosmosManager = _fixture.Container.Resolve<ICosmosManager>();
            var cosmosConfiguration = new Bullfrog.Actors.Interfaces.Models.CosmosConfiguration
            {
                Name = "test",
                AccountName = "bullfrog-cosmos-test",
                DatabaseName = TestDbName,
                MinimumRU = 300,    // pretend the db has been scaled out and it's new min throughput is 400
                MaximumRU = 600,
                RequestUnitsPerRequest = 20,
            };
            var rus = await cosmosManager.SetScale(10, cosmosConfiguration);

            rus.Should().Be(400);
            var newThoughput = await _fixture.GetCosmosClient().Databases[TestDbName].ReadProvisionedThroughputAsync();
            newThoughput.Should().Be(400);
        }

        [Fact, IsLayer1]
        public async Task ResetCosmosDbScale()
        {
            await CreateWithThroughput(500);

            var cosmosManager = _fixture.Container.Resolve<ICosmosManager>();
            var cosmosConfiguration = new Bullfrog.Actors.Interfaces.Models.CosmosConfiguration
            {
                Name = "test",
                AccountName = "bullfrog-cosmos-test",
                DatabaseName = TestDbName,
                MinimumRU = 400,
                MaximumRU = 600,
                RequestUnitsPerRequest = 20,
            };
            var rus = await cosmosManager.Reset(cosmosConfiguration);

            rus.Should().Be(400);
            var newThoughput = await _fixture.GetCosmosClient().Databases[TestDbName].ReadProvisionedThroughputAsync();
            newThoughput.Should().Be(400);
        }

        private async Task CreateWithThroughput(int thoughput)
        {
            await _fixture.CreateTemporaryDatabaseIfNotExists(TestDbName, thoughput);
            await _fixture.GetCosmosClient().Databases[TestDbName].ReplaceProvisionedThroughputAsync(thoughput);
            var currentThroughput = await _fixture.GetCosmosClient().Databases[TestDbName].ReadProvisionedThroughputAsync();
            currentThroughput.Should().Be(thoughput);
        }
    }
}
