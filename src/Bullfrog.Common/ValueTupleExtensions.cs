using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Bullfrog.Common
{
    [ExcludeFromCodeCoverage]
    public static class ValueTupleExtensions
    {
        public static KeyValuePair<TKey, TValue> ToKeyValuePair<TKey, TValue>(this ValueTuple<TKey, TValue> tuple)
            => new KeyValuePair<TKey, TValue>(tuple.Item1, tuple.Item2);
    }
}
