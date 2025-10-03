using SPTarkov.Server.Core.Models.Common;

namespace WTTServerCommonLib.Helpers;

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