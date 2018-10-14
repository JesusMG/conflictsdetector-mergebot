using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ConflictsBot
{
    internal static class FindQueries
    {
        internal static List<Branch> FindResolvedBranches(
            IRestApi restApi,
            string repository,
            string prefix,
            string statusAttributeName,
            string resolvedStatusAttributeValue)
        {
            string query = string.Format(
                "branch where ( name like '{0}%' or name like '{1}%' ) " +
                "and date > '{2}' and attribute='{3}' and ( attrvalue='{4}' or attrvalue='{5}')",
                prefix.ToLowerInvariant(),
                prefix.ToUpperInvariant(),
                DateTime.Now.AddYears(-1).ToString(UTC_SORTABLE_DATE_FORMAT),
                statusAttributeName,
                resolvedStatusAttributeValue.ToLowerInvariant(),
                resolvedStatusAttributeValue.ToUpperInvariant());

            JArray findResult = restApi.Find(
                repository,
                query,
                UTC_SORTABLE_DATE_FORMAT,
                "retrieve the list of branches to process",
                new string[] { "id", "name", "owner", "comment" });

            List<Branch> result = new List<Branch>();
            foreach(JObject obj in findResult)
            {
                result.Add(new Branch(
                    repository,
                    GetStringValue(obj, "id"),
                    GetStringValue(obj, "name"),
                    GetStringValue(obj, "owner"),
                    GetStringValue(obj, "comment")));
            }
            return result;
        }

        internal static string GetBranchName(
            IRestApi restApi, string repository, string branchId)
        {
            string query = string.Format("branch where id={0}", branchId);

            JArray findResult = restApi.Find(
                repository,
                query,
                UTC_SORTABLE_DATE_FORMAT,
                "retrieve a single branch by ID",
                new string[] { "name" });

            if (findResult.Count == 0)
                return string.Empty;

            return GetStringValue((JObject)findResult[0], "name");
        }

        static string GetStringValue(JObject obj, string fieldName)
        {
            object value = obj[fieldName];
            return value == null ? string.Empty : value.ToString();
        }

        const string UTC_SORTABLE_DATE_FORMAT = "yyyy-MM-ddTHH:mm:ssZ";
    }
}