using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using WTTServerCommonLib.Helpers;
using WTTServerCommonLib.Services;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;

namespace WTTServerCommonLib;
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.grooveypenguinx.WTTServerCommonLib";
    public override string Name { get; init; } = "WTTServerCommonLib";
    public override string Author { get; init; } = "GrooveypenguinX";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public override SemanticVersioning.Version SptVersion { get; init; } = new("4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Version>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "WTT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTServerCommonLib(
    WTTCustomItemServiceExtended customItemServiceExtended, 
    WTTCustomAssortSchemeService customAssortSchemeService, 
    WTTCustomLootspawnService customLootspawnService,
    WTTCustomQuestService customQuestService,
    WTTLocaleService localeService,
    WTTCustomHideoutRecipes hideoutRecipes,
    ISptLogger<WTTServerCommonLib> logger) : IOnLoad
{
    public WTTCustomItemServiceExtended CustomItemServiceExtended { get; } = customItemServiceExtended;
    public WTTCustomAssortSchemeService CustomAssortSchemeService { get; } = customAssortSchemeService;
    public WTTCustomLootspawnService CustomLootspawnService { get; } = customLootspawnService;
    public WTTCustomQuestService CustomQuestService { get; } = customQuestService;
    public WTTLocaleService LocaleService { get; } = localeService;
    public WTTCustomHideoutRecipes CustomHideoutRecipes { get; } = hideoutRecipes;
    
    
    public Task OnLoad()
    {
        return Task.CompletedTask;
    }
}
[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 1)]
public class WTTServerCommonLibPostSptLoad(WTTCustomItemServiceExtended customItemServiceExtended) : IOnLoad
{
    public Task OnLoad()
    {
        customItemServiceExtended.ProcessDeferredModSlots();
        return Task.CompletedTask;
    }
}