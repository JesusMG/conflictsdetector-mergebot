using System;
using System.IO;
using System.Reflection;

namespace ConflictsBot
{
    internal static class ToolConfig
    {
        internal static string GetLogConfigFile()
        {
            return GetConfigFilePath(LOG_CONFIG_FILE);
        }

        internal static string GetResolvedBranchesStorageFile(string botName)
        {
            string branchesFileName = string.Format(RESOLVED_BRANCHES_FILE, botName);

            return GetConfigFilePath(branchesFileName);
        }

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

        const string RESOLVED_BRANCHES_FILE = "resolved_branches.{0}.txt";
        const string LOG_CONFIG_FILE = "conflictsbot.log.conf";
        const string CONFIG_FOLDER_NAME = "config";
    }
}