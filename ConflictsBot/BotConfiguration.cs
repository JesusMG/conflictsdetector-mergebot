using System;
using Newtonsoft.Json.Linq;

using log4net;

namespace ConflictsBot
{
    internal class BotConfiguration
    {
        internal static BotConfiguration Build(
            string configFile,
            string restApiServerUrl,
            string webSocketServerUrl)
        {
            string fileContent = System.IO.File.ReadAllText(configFile);
            JObject config = JObject.Parse(fileContent);

            if (config == null)
                return null;

            string repository = Field.GetString(config,"repository");

            string branchPrefix = Field.GetString(config,"branchPrefix");

            string trunkBranch = Field.GetString(config, "trunkBranch");

            string plasticBotUserToken = Field.GetString(config, "plasticBotUserToken");

            StatusProperty statusAttrConfig = StatusProperty.BuildFromConfig(config["plasticStatusAttributeGroup"]);

            IssueTracker issueTrackerConfig = IssueTracker.BuildFromConfig(config["issuesGroup"]);

            Notifier emailConfig = Notifier.BuildFromConfig(config["notifierEmailGroup"]);

            return new BotConfiguration(
                restApiServerUrl,
                webSocketServerUrl,
                repository,
                trunkBranch,
                branchPrefix,
                plasticBotUserToken,
                statusAttrConfig,
                issueTrackerConfig,
                emailConfig);
        }

        internal BotConfiguration(
            string restApiServerUrl,
            string webSocketServerUrl,
            string repository,
            string trunkBranch,
            string branchPrefix,
            string plasticBotUserToken,
            StatusProperty plsticStatusAttrConfig,
            IssueTracker issueTrackerConfig,
            Notifier notifierConfig)
        {
            RestApiUrl = restApiServerUrl;
            WebSocketUrl = webSocketServerUrl;
            Repository = repository;
            TrunkBranch = trunkBranch;
            BranchPrefix = branchPrefix;
            PlasticBotUserToken = plasticBotUserToken;

            PlasticStatusAttrConfig = plsticStatusAttrConfig;
            IssueTrackerConfig = issueTrackerConfig;
            NotifierConfig = notifierConfig;
        }

        internal string RestApiUrl { get; private set; }

        internal string WebSocketUrl { get; private set; }

        internal string Repository { get; private set; }

        internal string TrunkBranch { get; private set; }

        internal string BranchPrefix { get; private set; }

        internal string PlasticBotUserToken { get; private set; }

        internal StatusProperty PlasticStatusAttrConfig { get; private set; }

        internal IssueTracker IssueTrackerConfig { get; private set; }

        internal Notifier NotifierConfig { get; private set; }

        internal class IssueTracker
        {
            internal readonly string PlugName;
            internal readonly string ProjectKey;
            internal readonly StatusProperty StatusField;

            internal IssueTracker(
                string plugName,
                string projectKey,
                StatusProperty statusField)
            {
                PlugName = plugName;
                ProjectKey = projectKey;
                StatusField = statusField;
            }

            internal static IssueTracker BuildFromConfig(JToken jToken)
            {
                string plugName = Field.GetString(jToken, "plugName");
                if (string.IsNullOrEmpty(plugName) || plugName.ToLowerInvariant().Trim().Equals("none"))
                    return null;

                IssueTracker result = new IssueTracker(
                    plugName,
                    Field.GetString(jToken, "projectKey"),
                    StatusProperty.BuildFromConfig(jToken["statusFieldGroup"]));

                return result;
            }
        }

        internal class StatusProperty
        {
            internal readonly string Name;
            internal readonly string ResolvedValue;
            internal readonly string FailedValue;            
            internal readonly string MergedValue;

            StatusProperty(
                string name, 
                string resolvedValue, 
                string failedValue, 
                string mergedValue)
            {
                Name = name;
                ResolvedValue = resolvedValue;
                FailedValue = failedValue;
                MergedValue = mergedValue;
            }

            internal static StatusProperty BuildFromConfig(JToken jToken)
            {
                return new StatusProperty(
                    Field.GetString(jToken, "statusAttribute"),
                    Field.GetString(jToken, "resolvedValue"),
                    Field.GetString(jToken, "failedValue"),
                    Field.GetString(jToken, "mergedValue"));
            }
        }

        internal class Notifier
        {
            internal readonly string PlugName;
            internal readonly string UserProfileField;
            internal readonly string[] FixedRecipients;

            internal Notifier(string plugName, string userProfileField, string[] fixedRecipients)
            {
                PlugName = plugName;
                UserProfileField = userProfileField;
                FixedRecipients = fixedRecipients;
            }

            internal static Notifier BuildFromConfig(JToken jToken)
            {
                string plugName = Field.GetString(jToken, "plugName");

                if (string.IsNullOrEmpty(plugName) || plugName.ToLowerInvariant().Trim().Equals("none"))
                    return null;

                string[] rawRecipientsArray =
                    Field.GetString(jToken, "fixedRecipientsPlasticUsers").
                    Split(
                        new char[] { ';', ',' },
                        System.StringSplitOptions.RemoveEmptyEntries);

                string[] normalizedRecipientsArray = new string[rawRecipientsArray.Length];

                for (int i = 0; i < rawRecipientsArray.Length; i++)
                    normalizedRecipientsArray[i] = rawRecipientsArray[i].Trim();
                
                return new Notifier(
                    Field.GetString(jToken, "plugName"),
                    Field.GetString(jToken, "userProfileFieldName"),
                    normalizedRecipientsArray);
            }
        }
        internal static class Field
        {
            internal static string GetString(JToken section, string fieldName)
            {
                if (section == null || section[fieldName] == null)
                {
                    LogUndefinedParam(section, fieldName, string.Empty);
                    return string.Empty;
                }

                string fieldValue = section[fieldName].Value<string>();

                LogConfigParam(section.Path, fieldName, fieldValue);

                return fieldValue;
            }

            static void LogConfigParam(string sectionPath, string fieldName, string fieldValue)
            {
                mLog.DebugFormat("Config:[{0}.{1}]={2}", sectionPath, fieldName, fieldValue);
            }

            static void LogUndefinedParam(JToken section, string fieldName, string fallbackValue)
            {
                mLog.WarnFormat("Config:[{0}{1}]=UNDEFINED -> FALLBACK VALUE=[{2}]",
                    section == null || string.IsNullOrEmpty(section.Path) ?
                        string.Empty :
                        section.Path + ".",
                    fieldName,
                    fallbackValue);
            }

            static readonly ILog mLog = LogManager.GetLogger(typeof(Field));        
        }
    }
}