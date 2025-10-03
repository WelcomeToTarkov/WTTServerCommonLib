using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Server;
using WTTServerCommonLib.Constants;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Models;


namespace WTTServerCommonLib.Services.ItemServiceHelpers;

public static class TraderItemHelper
{
        public static void AddItem(CustomItemConfig config, string itemId, DatabaseTables database)
        {
            try
            {
                if (config.Traders == null || config.Traders.Count == 0)
                {
                    Log.Warn( $"No trader entries for item {itemId}");
                    return;
                }

                foreach (var (traderKey, schemes) in config.Traders)
                {
                    if (!TraderIds.TraderMap.TryGetValue(traderKey, out var traderId))
                    {
                        Log.Warn($"Unknown trader key '{traderKey}'");
                        continue;
                    }

                    if (!database.Traders.TryGetValue(traderId, out var trader))
                    {
                        Log.Warn( $"Trader {traderId} not found in DB for item {itemId}");
                        continue;
                    }

                    foreach (var (schemeKey, scheme) in schemes)
                    {
                        var newItem = new Item
                        {
                            Id = schemeKey,
                            Template = itemId,
                            ParentId = "hideout",
                            SlotId = "hideout",
                            Upd = new Upd
                            {
                                UnlimitedCount = scheme.BarterSettings.UnlimitedCount,
                                StackObjectsCount = scheme.BarterSettings.StackObjectsCount
                            }
                        };

                        trader.Assort.Items.Add(newItem);

                        if (!trader.Assort.BarterScheme.TryGetValue(schemeKey, out var barterOptions))
                        {
                            barterOptions = new List<List<BarterScheme>>();
                            trader.Assort.BarterScheme[schemeKey] = barterOptions;
                        }

                        var barters = scheme.Barters;
                        var barterSchemeItems = new List<BarterScheme>();

                        foreach (var b in barters)
                        {
                            if (string.IsNullOrWhiteSpace(b.Template)) continue;

                            var barter = new BarterScheme
                            {
                                Count = b.Count,
                                Template = ItemTplResolver.ResolveId(b.Template)
                            };

                            if (b.Level != null) barter.Level = b.Level;
                            if (b.OnlyFunctional != null) barter.OnlyFunctional = b.OnlyFunctional;
                            if (b.Side != null) barter.Side = b.Side;
                            if (b.SptQuestLocked != null) barter.SptQuestLocked = b.SptQuestLocked;

                            barterSchemeItems.Add(barter);
                        }

                        if (barterSchemeItems.Count > 0)
                        {
                            barterOptions.Add(barterSchemeItems);
                        }

                        trader.Assort.LoyalLevelItems[schemeKey] = scheme.BarterSettings.LoyalLevel;

                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error( $"Error adding {itemId} to traders: {ex}");
            }
        }
        
}