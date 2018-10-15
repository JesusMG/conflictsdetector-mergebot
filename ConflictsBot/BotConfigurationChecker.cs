namespace ConflictsBot
{
    internal class BotConfigurationChecker
    {
        internal static bool CheckConfiguration(
            BotConfiguration botConfig, out string errorMessage)
        {
            if (!CheckValidFields(botConfig, out errorMessage))
            {
                errorMessage = string.Format(
                    "trunkbot can't start without specifying a valid config for the following fields:\n{0}",
                    errorMessage);
                return false;
            }

            return true;
        }

        internal static bool CheckValidFields(
            BotConfiguration botConfig,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrEmpty(botConfig.Repository))
                errorMessage += BuildFieldError("repository");

            if (string.IsNullOrEmpty(botConfig.TrunkBranch))
                errorMessage += BuildFieldError("trunk branch");

            if (string.IsNullOrEmpty(botConfig.BranchPrefix))
                errorMessage += BuildFieldError("branch prefix");

            if (string.IsNullOrEmpty(botConfig.PlasticBotUserToken))
                errorMessage += BuildFieldError("user api key");

            string propertyErrorMessage = null;
            if (!CheckValidPlasticStatusFields(botConfig.PlasticStatusAttrConfig, out propertyErrorMessage))
                errorMessage += propertyErrorMessage;

            propertyErrorMessage = null;
            if (!CheckValidIssueTrackerFields(botConfig.IssueTrackerConfig, out propertyErrorMessage))
                errorMessage += propertyErrorMessage;

            propertyErrorMessage = null;
            if (!CheckValidNotifierFields(botConfig.NotifierConfig, out propertyErrorMessage))
                errorMessage += propertyErrorMessage;

            return string.IsNullOrEmpty(errorMessage);
        }

        static bool CheckValidPlasticStatusFields(
            BotConfiguration.StatusProperty plasticAttributeConfig,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (plasticAttributeConfig == null)
            {
                errorMessage = BuildFieldError("Plastic SCM status attribute configuration");
                return false;
            }

            string propertyErrorMessage = null;
            if (!CheckValidStatusPropertyFields(
                    plasticAttributeConfig,
                    "of the status attribute for Plastic config",
                    true,
                    out propertyErrorMessage))
            {
                errorMessage += propertyErrorMessage;
            }

            return string.IsNullOrEmpty(errorMessage);
        }

        static bool CheckValidIssueTrackerFields(
            BotConfiguration.IssueTracker issueTrackerConfig,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (issueTrackerConfig == null)
                return true;

            if (string.IsNullOrEmpty(issueTrackerConfig.PlugName))
                errorMessage += BuildFieldError("plug name for Issue Tracker config");

            string propertyErrorMessage = null;
            if (!CheckValidStatusPropertyFields(
                    issueTrackerConfig.StatusField,
                    "of the status field for Issue Tracker config",
                    false,
                    out propertyErrorMessage))
                errorMessage += propertyErrorMessage;

            return string.IsNullOrEmpty(errorMessage);
        }

        static bool CheckValidNotifierFields(
            BotConfiguration.Notifier notifierConfig,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (notifierConfig == null)
                return true;

            if (string.IsNullOrEmpty(notifierConfig.PlugName))
                errorMessage += BuildFieldError("plug name for Notifications config");

            if (IsDestinationInfoEmpty(notifierConfig))
            {
                errorMessage += "* There is no destination info in the Notifications" +
                    " config. Please specify a user profile field, a list of recipients" +
                    " or both (recommended).\n";
            }

            return string.IsNullOrEmpty(errorMessage);
        }

        static bool CheckValidStatusPropertyFields(
            BotConfiguration.StatusProperty statusAttributeConfig,
            string groupNameMessage,
            bool bCheckMergedValue,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrEmpty(statusAttributeConfig.Name))
                errorMessage += BuildFieldError("name " + groupNameMessage);

            if (string.IsNullOrEmpty(statusAttributeConfig.ResolvedValue))
                errorMessage += BuildFieldError("resolved value " + groupNameMessage);

            if (string.IsNullOrEmpty(statusAttributeConfig.FailedValue))
                errorMessage += BuildFieldError("failed value " + groupNameMessage);

            if (!bCheckMergedValue)
                return string.IsNullOrEmpty(errorMessage);

            if (string.IsNullOrEmpty(statusAttributeConfig.MergedValue))
                errorMessage += BuildFieldError("merged value " + groupNameMessage);

            return string.IsNullOrEmpty(errorMessage);
        }

        static bool IsDestinationInfoEmpty(BotConfiguration.Notifier notifierConfig)
        {
            return string.IsNullOrEmpty(notifierConfig.UserProfileField) &&
                (notifierConfig.FixedRecipients == null || notifierConfig.FixedRecipients.Length == 0);
        }

        static string BuildFieldError(string fieldName)
        {
            return string.Format("* The {0} must be defined.\n", fieldName);
        }
    }
}