using SPTarkov.Server.Core.Models.Utils;
using LogLevel = SPTarkov.Server.Core.Models.Spt.Logging.LogLevel;


namespace WTTServerCommonLib.Helpers;

public static class Log
{
    public static ISptLogger<WTTServerCommonLib>? Instance { get; private set; }

    public static void Init(ISptLogger<WTTServerCommonLib> logger)
    {
        Instance = logger;
    }

    public static void Info(string message) => Instance?.Log(LogLevel.Info, message);
    public static void Error(string message) => Instance?.Log(LogLevel.Error, message);
    public static void Warn(string message) => Instance?.Log(LogLevel.Warn, message);
    public static void Debug(string message) => Instance?.Log(LogLevel.Debug, message);
}