using log4net;

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
            try
            {
                restApi.UpdateBranchAttribute(repository, branch, attrName, attrValue);
            }
            catch (System.Exception e)
            {
                mLog.ErrorFormat("Error updating branch attribute:{0}", e.Message);
            }
        }

        internal static void UpdateIssueTrackerField(
            IRestApi restApi,
            string plugName,
            string projectKey, 
            string taskNumber, 
            string fieldName, 
            string fieldValue)
        {
            try
            {
                restApi.UpdateIssueTrackerField(plugName, projectKey, taskNumber, fieldName, fieldValue);
            }
            catch (System.Exception e)
            {
                mLog.ErrorFormat("Error updating issue tracker field:{0}", e.Message);
            }
        }

        static readonly ILog mLog = LogManager.GetLogger(typeof(StatusUpdater));
    }

}