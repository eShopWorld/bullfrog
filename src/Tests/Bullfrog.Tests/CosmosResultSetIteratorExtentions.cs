using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

public static class CosmosResultSetIteratorExtentions
{
    public static async Task<List<T>> ToListAsync<T>(this CosmosResultSetIterator<T> iterator, CancellationToken cancellationToken = default)
    {
        var list = new List<T>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.FetchNextSetAsync(cancellationToken);
            list.AddRange(response);
        }

        return list;
    }
}
