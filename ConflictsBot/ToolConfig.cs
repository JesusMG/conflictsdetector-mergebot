using System;
using System.IO;
using System.Reflection;

namespace ConflictsBot
{
    internal static class ToolConfig
    {
        internal static string GetHalBotLogConfigFile() => GetConfigFilePath(LOG_CONFIG_FILE);

        static string GetConfigFilePath(string configfile)
        {
            return Path.Combine(GetConfigDirectory(), configfile);
        }

        static string GetConfigDirectory()
        {
            string appPath = Path.GetDirectoryName(
                Assembly.GetExecutingAssembly().Location);

            return Path.Combine(appPath, CONFIG_FOLDER_NAME);
        }

        const string LOG_CONFIG_FILE = "conflictsbot.log.conf";
        const string CONFIG_FOLDER_NAME = "config";
    }
}