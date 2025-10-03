namespace WTTServerCommonLib.Models;

public static class TraderIds
{
    public static readonly Dictionary<string, string> TraderMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Mechanic",    "5a7c2eca46aef81a7ca2145d" },
        { "Skier",       "58330581ace78e27b8b10cee" },
        { "Peacekeeper", "5935c25fb3acc3127c3d8cd9" },
        { "Therapist",   "54cb57776803fa99248b456e" },
        { "Prapor",      "54cb50c76803fa8b248b4571" },
        { "Jaegar",      "5c0647fdd443bc2504c2d371" },
        { "Ragman",      "5ac3b934156ae10c4430e83c" },
        { "Fence",       "579dc571d53a0658a154fbec" },
        { "Badger",      "bd3a8b28356d9c6509966546" }
    };

    public static void Add(string traderName, string traderId)
    {
        TraderMap[traderName] = traderId;
    }
}