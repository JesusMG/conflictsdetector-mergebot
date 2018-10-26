using System;
using System.Collections.Generic;
using System.Net.Http;

using log4net;

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

            string headingMessage = string.IsNullOrEmpty(notificationConfig.IntroMessage) ?
                string.Empty : notificationConfig.IntroMessage;

            string trailingMessage = (string.IsNullOrEmpty(notificationConfig.TrailingMessage) || bIsSuccessfulMerge) ?
                string.Empty : TransformMessage(notificationConfig.TrailingMessage);

            string messageWithIntro = string.Format(
                "{0}{1}{1}{2}{1}{1}{3}",
                headingMessage,
                System.Environment.NewLine,
                message,
                trailingMessage);

            List<string> recipients = new List<string>();
            recipients.Add(owner);
            recipients.AddRange(notificationConfig.FixedRecipients);

            restApi.Notify(
                notificationConfig.PlugName,
                messageWithIntro,
                Profile.ResolveUserField(
                    restApi, recipients, notificationConfig.UserProfileField));
        }

        static string TransformMessage(string trailingMessage)
        {            
            string transformedMessage = trailingMessage;
            while (transformedMessage.IndexOf(START_VARIABLE) >= 0)
            {
                int startIndex = transformedMessage.IndexOf(START_VARIABLE);
                int endIndex = transformedMessage.IndexOf(END_VARIABLE, startIndex);

                if (endIndex < startIndex)
                    return string.Empty;

                string variable = transformedMessage.Substring(
                    startIndex, endIndex + END_VARIABLE.Length - startIndex);

                if (HasAnyVariableToken(variable))
                    return string.Empty;

                transformedMessage = transformedMessage.Remove(
                    startIndex, endIndex + END_VARIABLE.Length - startIndex);

                transformedMessage = transformedMessage.Insert(startIndex, ReplaceVariable(variable));
            }

            return transformedMessage;
        }

        static string ReplaceVariable(string varUrl)
        {
            string url = varUrl.
                Replace(START_VARIABLE, string.Empty).
                Replace(END_VARIABLE, string.Empty).
                Trim();
            
            return GetCustomMessageFromApi(url);
        }

        static bool HasAnyVariableToken(string variable)
        {
            variable = variable.Substring(
                START_VARIABLE.Length, 
                variable.Length - START_VARIABLE.Length - END_VARIABLE.Length);

            if (variable.IndexOf(START_VARIABLE) >= 0 || variable.IndexOf(END_VARIABLE) >= 0)
                return true;

            return false;
        }
		
		static string GetCustomMessageFromApi(string Url)
        {
            try
            {
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(Url);
                HttpResponseMessage response = client.GetAsync(Url).Result;
                if (response.IsSuccessStatusCode)
                {
                    return response.Content.ReadAsStringAsync().Result;
                }
                return string.Empty;
            }
            catch (System.Exception e)
            {
                mLog.WarnFormat("Unable to complete a GET request to: {0}. {1}", Url, e.Message);
                return string.Empty;
            }
        }

        static readonly ILog mLog = LogManager.GetLogger(typeof(Notifier));

        const string START_VARIABLE = "${";
        const string END_VARIABLE = "}";

    }
}