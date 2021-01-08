using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bullfrog.Common.DependencyInjection;
using Bullfrog.Common.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Bullfrog.Tests.Components
{
    [Collection(CosmosFixtureCollection.TestFixtureName)]
    public class CosmosDataPlaneAccessValidatorTests
    {
        private readonly CosmosFixture _cosmosFixture;

        public CosmosDataPlaneAccessValidatorTests(CosmosFixture cosmosFixture)
        {
            _cosmosFixture = cosmosFixture;
        }

        [Fact, IsLayer2]
        public async Task ValidatesIsSuccessfulTest()
        {
            var db = await _cosmosFixture.GetTestCosmos();
            var dataPlaneConnection = new CosmosDbDataPlaneConnection
            {
                AccountName = "account",
                DatabaseName = db.DatabaseName,
            };
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("cm:cosmos-connection:account", db.ConnectionString),
            });
            var validator = new CosmosDataPlaneAccessValidator(configurationBuilder.Build());

            var result = await validator.ConfirmAccess(dataPlaneConnection);

            result.Should().Be(ValidationResult.Success);
        }


        [Fact, IsLayer2]
        public async Task ValidatesFailsBecauseOfAccountNameTest()
        {
            var dataPlaneConnection = new CosmosDbDataPlaneConnection
            {
                AccountName = "account", // not found in configuration
                DatabaseName = "name",
            };
            var configurationBuilder = new ConfigurationBuilder();
            var validator = new CosmosDataPlaneAccessValidator(configurationBuilder.Build());

            var result = await validator.ConfirmAccess(dataPlaneConnection);

            result.Should().NotBe(ValidationResult.Success);
            result.MemberNames.Should().ContainSingle(nameof(CosmosDbDataPlaneConnection.AccountName));
        }

        [Fact, IsLayer2]
        public async Task ValidatesFailsBecauseOfInvalidAccountKeyTest()
        {
            var db = await _cosmosFixture.GetTestCosmos();
            var dataPlaneConnection = new CosmosDbDataPlaneConnection
            {
                AccountName = "account",
                DatabaseName = db.DatabaseName,
            };
            var badConnectionString = CosmosFixture.ModifyConnectionString(db.ConnectionString, "AccountKey", "bad");
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("cm:cosmos-connection:account", badConnectionString),
            });
            var validator = new CosmosDataPlaneAccessValidator(configurationBuilder.Build());

            var result = await validator.ConfirmAccess(dataPlaneConnection);

            result.Should().NotBe(ValidationResult.Success);
            result.MemberNames.Should().BeEmpty();
        }

        [Fact, IsLayer2]
        public async Task ValidatesFailsBecauseOfDatabaseNameTest()
        {
            var dataPlaneConnection = new CosmosDbDataPlaneConnection
            {
                AccountName = "account",
                DatabaseName = "wrongDb",
            };
            var db = await _cosmosFixture.GetTestCosmos();
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("cm:cosmos-connection:account", db.ConnectionString),
            });
            var validator = new CosmosDataPlaneAccessValidator(configurationBuilder.Build());

            var result = await validator.ConfirmAccess(dataPlaneConnection);

            result.Should().NotBe(ValidationResult.Success);
            result.MemberNames.Should().ContainSingle(nameof(CosmosDbDataPlaneConnection.DatabaseName));
        }

        [Fact, IsLayer2]
        public async Task ValidatesFailsBecauseOfContainerNameTest()
        {
            var db = await _cosmosFixture.GetTestCosmos();
            var dataPlaneConnection = new CosmosDbDataPlaneConnection
            {
                AccountName = "account",
                DatabaseName = db.DatabaseName,
                ContainerName = "unknown",
            };
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("cm:cosmos-connection:account", db.ConnectionString),
            });
            var validator = new CosmosDataPlaneAccessValidator(configurationBuilder.Build());

            var result = await validator.ConfirmAccess(dataPlaneConnection);

            result.Should().NotBe(ValidationResult.Success);
            result.MemberNames.Should().ContainSingle(nameof(CosmosDbDataPlaneConnection.ContainerName));
        }

        [Fact, IsLayer2]
        public async Task ValidatesFailsBecauseOfContainerThroughputNotAssignedTest()
        {
            var db = await _cosmosFixture.GetTestCosmos();
            var dataPlaneConnection = new CosmosDbDataPlaneConnection
            {
                AccountName = "account",
                DatabaseName = db.DatabaseName,
                ContainerName = db.ContainerName,
            };
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string>("cm:cosmos-connection:account", db.ConnectionString),
            });
            var validator = new CosmosDataPlaneAccessValidator(configurationBuilder.Build());

            var result = await validator.ConfirmAccess(dataPlaneConnection);

            result.Should().NotBe(ValidationResult.Success);
            result.MemberNames.Should().ContainSingle(nameof(CosmosDbDataPlaneConnection.ContainerName));
        }
    }
}
