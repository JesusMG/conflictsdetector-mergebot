using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ConflictsBot
{
    internal static class Profile
    {
        internal static List<string> ResolveUserField(
            IRestApi restApi, List<string> users, string profileFieldQualifiedName)
        {
            string[] profileFieldsPath = profileFieldQualifiedName.Split(
                new char[] { '.' },
                System.StringSplitOptions.RemoveEmptyEntries);

            List<string> result = new List<string>();

            foreach (string user in users)
            {
                JObject profile = restApi.GetUserProfile(user);
                if (profile == null || !profile.HasValues)
                {
                    result.Add(user);
                    continue;
                }

                string solvedUser = GetFieldFromProfile(profile, profileFieldsPath);

                if (string.IsNullOrEmpty(solvedUser))
                    continue;

                result.Add(solvedUser);
            }

            return result;
        }

        static string GetFieldFromProfile(
            JObject userProfile, string[] profileFieldsPath)
        {
            if (profileFieldsPath.Length == 0)
                return null;

            if (userProfile == null)
                return null;

            JToken currentField = userProfile;
            foreach (string field in profileFieldsPath)
            {
                currentField = currentField[field];

                if (currentField == null)
                    return null;
            }

            if (currentField.Type != JTokenType.String)
                return null;

            return currentField.Value<string>();
        }
    }
}
