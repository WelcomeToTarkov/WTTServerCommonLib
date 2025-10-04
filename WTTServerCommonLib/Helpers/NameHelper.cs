using System.Reflection;
using SPTarkov.Server.Core.Models.Common;

namespace WTTServerCommonLib.Helpers;


public static class ItemTplResolver
{
    private static readonly Dictionary<string, MongoId> _cache = new Dictionary<string, MongoId>(StringComparer.OrdinalIgnoreCase);
    private static bool _isInitialized = false;

    static ItemTplResolver()
    {
        InitializeCache();
    }

    private static void InitializeCache()
    {
        if (_isInitialized) return;
        
        var fields = typeof(ItemTpl).GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(MongoId))
            {
                _cache[field.Name] = (MongoId)field.GetValue(null);
            }
        }
        
        _isInitialized = true;
    }

    public static MongoId ResolveId(string itemName)
    {
        if (_cache.TryGetValue(itemName, out var mongoId))
        {
            return mongoId;
        }
        
        throw new ArgumentException($"Item template '{itemName}' not found in ItemTpl class");
    }

    public static bool TryResolveId(string itemName, out MongoId result)
    {
        return _cache.TryGetValue(itemName, out result);
    }
}
public static class NameHelper
{
    public static string ResolveId(string keyOrId, Dictionary<string, MongoId> map)
    {
        if (string.IsNullOrWhiteSpace(keyOrId))
            throw new ArgumentNullException(nameof(keyOrId), "ResolveId received null or empty value.");

        if (MongoId.IsValidMongoId(keyOrId))
            return keyOrId;

        if (map.TryGetValue(keyOrId, out var resolved))
        {
            var resolvedStr = resolved.ToString();
            if (!MongoId.IsValidMongoId(resolvedStr))
                throw new ArgumentException($"Invalid ObjectId in map for '{keyOrId}': {resolvedStr}");

            return resolvedStr;
        }

        throw new ArgumentException($"'{keyOrId}' is not a valid ObjectId and was not found in the map.");
    }
}