using System.Collections.Generic;

namespace Bullfrog.Common
{
    public static class DictionaryExtensions
    {
        public static void Append<TKey, TItem>(this Dictionary<TKey, List<TItem>> dict, TKey key, TItem item)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<TItem>();
                dict.Add(key, list);
            }
            list.Add(item);
        }

        public static void Append<TKey, TItem>(this Dictionary<TKey, TItem[]> dict, TKey key, TItem item)
        {
            if (!dict.TryGetValue(key, out var array))
            {
                dict.Add(key, new TItem[] { item });
            }
            else
            {
                var newArray = new TItem[array.Length + 1];
                array.CopyTo(newArray, 0);
                newArray[array.Length] = item;
                dict[key] = newArray;
            }
        }
    }
}
