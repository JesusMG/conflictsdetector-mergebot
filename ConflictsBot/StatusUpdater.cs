namespace ConflictsBot
{
    internal static class StatusUpdater
    {
        internal static void UpdateBranchAttribute(
            IRestApi restApi, 
            string repository,
            string branch,
            string attrName,
            string attrValue)
        {
            restApi.UpdateBranchAttribute(repository, branch, attrName, attrValue);
        }

        internal static void UpdateIssueTrackerField(
            IRestApi restApi,
            string plugName,
            string projectKey, 
            string taskNumber, 
            string fieldName, 
            string fieldValue)
        {
            restApi.UpdateIssueTrackerField(plugName, projectKey, taskNumber, fieldName, fieldValue);
        }
    }

}