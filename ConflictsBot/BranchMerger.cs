using System;

using log4net;

namespace ConflictsBot
{
    internal static class BranchMerger
    {
        internal class Result
        {
            internal bool HasManualConflicts;
            internal string Message;
        }

        internal static Result Try(
			IRestApi restApi, 
			string repository, 
			string srcTaskBranch, 
			string dstTrunkBranch)
        {
            int shelveId = 0;
            Result opResult = new Result();

            try
            {
                RestApi.MergeToResponse mergeToResult = restApi.MergeBranchToShelve(repository, srcTaskBranch, dstTrunkBranch);

                mLog.DebugFormat(
                    "Try merge from branch {0} to branch {1} finished with status {2} and message: {3}", 
                    srcTaskBranch, dstTrunkBranch, mergeToResult.Status, mergeToResult.Message);

                if (mergeToResult.Status == RestApi.MergeToResponse.MergeToResultStatus.OK ||
                    mergeToResult.Status ==  RestApi.MergeToResponse.MergeToResultStatus.MergeNotNeeded)
                {                
                    shelveId = mergeToResult.ChangesetNumber;
                    opResult.HasManualConflicts = false;
                    return opResult;
                }

                opResult.HasManualConflicts = true;
                opResult.Message = mergeToResult.Message;
                return opResult;
            }
            finally
            {
                if (shelveId != 0)
                    SafeDeleteShelve(restApi, repository, shelveId);
            }
        }

        static void SafeDeleteShelve(IRestApi restApi, string repository, int shelveId)
        {
            try
            {
                restApi.DeleteShelve(restApi, repository, shelveId);
            }
            catch (Exception e)
            {
                mLog.ErrorFormat(
                    "Unable to delete shelve {0} on repository '{1}': {2}",
                    shelveId, repository, e.Message);

                mLog.DebugFormat(
                    "StackTrace:{0}{1}",
                    Environment.NewLine, e.StackTrace);
            }
        }

        static readonly ILog mLog = LogManager.GetLogger(typeof(BranchMerger));
    }
}