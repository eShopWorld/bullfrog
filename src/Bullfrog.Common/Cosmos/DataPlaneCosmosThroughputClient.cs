using System;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;

namespace Bullfrog.Common.Cosmos
{
    internal class DataPlaneCosmosThroughputClient : ICosmosThroughputClient
    {
        private readonly string _connectionString;
        private readonly string _databaseName;
        private readonly string _collectionName;

        public DataPlaneCosmosThroughputClient(string connectionString, string databaseName, string collectionName)
        {
            _connectionString = connectionString;
            _databaseName = databaseName;
            _collectionName = collectionName;
        }

        public async Task<CosmosThroughput> Get()
        {
            using (var client = OpenDocumentClient())
            {
                var offerResponse = await ReadOffer(client);
                return ReadThroughputDetails(offerResponse);
            }
        }

        public async Task<CosmosThroughput> Set(int throughput)
        {
            using (var client = OpenDocumentClient())
            {
                var offerResponse = await ReadOffer(client);
                var newOffer = new OfferV2(offerResponse.Resource, throughput);

                var response = await client.ReplaceOfferAsync(newOffer);
                return ReadThroughputDetails(response);
            }
        }

        private static CosmosThroughput ReadThroughputDetails(ResourceResponse<Offer> offerResponse)
        {
            using (var memoryStream = new System.IO.MemoryStream())
            {
                offerResponse.Resource.SaveTo(memoryStream);
                var buffer = memoryStream.GetBuffer();
                string content = Encoding.UTF8.GetString(buffer, 0, (int)memoryStream.Length);
                dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(content);
                var throughputDetails = new CosmosThroughput();
                throughputDetails.RequestsUnits = (int)json.content.offerThroughput;
                // The local CosmosDb emulator never returns max throughput ever provisioned.
                throughputDetails.MaxRequestUnitsEverProvisioned = (int?)json.content.offerMinimumThroughputParameters?.maxThroughputEverProvisioned ?? 0;
                // When setting an offer te local CosmosDb emulator does not return this value.
                if (!int.TryParse(offerResponse.ResponseHeaders["x-ms-cosmos-min-throughput"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minThroughput))
                    minThroughput = 400;
                throughputDetails.MinimalRequestUnits = minThroughput;
                throughputDetails.IsThroughputChangePending = (bool?)json.content.isOfferReplacePending ?? false;
                return throughputDetails;
            }
        }

        private async Task<ResourceResponse<Offer>> ReadOffer(DocumentClient client)
        {
            Resource resource;
            if (_collectionName != null)
            {
                var collectionUri = UriFactory.CreateDocumentCollectionUri(_databaseName, _collectionName);
                resource = await client.ReadDocumentCollectionAsync(collectionUri);
            }
            else
            {
                var databaseUri = UriFactory.CreateDatabaseUri(_databaseName);
                resource = await client.ReadDatabaseAsync(databaseUri);
            }

            var sqlQuerySpec = new SqlQuerySpec("SELECT * FROM offers o WHERE o.resource = @dbLink",
                new SqlParameterCollection(new SqlParameter[] { new SqlParameter { Name = "@dbLink", Value = resource.SelfLink } }));
            Offer offer = client.CreateOfferQuery(sqlQuerySpec).AsEnumerable().FirstOrDefault();
            return await client.ReadOfferAsync(offer.SelfLink);
        }

        private DocumentClient OpenDocumentClient()
        {
            var (serviceEndpoint, authKey) = ParseConnectionString(_connectionString);
            return new DocumentClient(serviceEndpoint, authKey);
        }

        private (Uri serviceEndpoint, string authKey) ParseConnectionString(string connectionString)
        {
            // Use this generic builder to parse the connection string
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            builder.TryGetValue("AccountKey", out object authKey);
            builder.TryGetValue("AccountEndpoint", out object serviceEndpoint);
            if (authKey == null || serviceEndpoint == null)
            {
                throw new BullfrogException("The connection string is invalid.");
            }

            return (new Uri(serviceEndpoint.ToString()), authKey.ToString());
        }
    }
}
