using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using WTTServerCommonLib.Models;
using Path = System.IO.Path;

namespace WTTServerCommonLib.Services;

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class WTTCustomBotLoadoutService(
    DatabaseServer databaseServer,
    ISptLogger<WTTCustomItemServiceExtended> logger,
    JsonUtil jsonUtil,
    ModHelper modHelper)
{
    public void RegisterCustomBotLoadouts()
    {
        var botTypes = databaseServer.GetTables().Bots.Types;
        foreach (var botType in botTypes)
        {}
    }
}