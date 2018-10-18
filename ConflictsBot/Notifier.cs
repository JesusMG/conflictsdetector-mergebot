using System.Collections.Generic;

namespace ConflictsBot
{
    internal static class Notifier
    {
        public static void Notify(
            IRestApi restApi,
            string owner,
            string message,
            BotConfiguration.Notifier notificationConfig,
            bool bIsSuccessfulMerge)
        {
            if (notificationConfig == null)
                return;

            if (bIsSuccessfulMerge && !notificationConfig.HasToNofifyOnSuccessfulTryMerge)
                return;

            List<string> recipients = new List<string>();
            recipients.Add(owner);
            recipients.AddRange(notificationConfig.FixedRecipients);

            restApi.Notify(
                notificationConfig.PlugName,
                message,
                Profile.ResolveUserField(
                    restApi, recipients, notificationConfig.UserProfileField));
        }
    }
}