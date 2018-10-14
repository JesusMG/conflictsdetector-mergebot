using System;
using System.Collections.Generic;
using System.Threading;
using log4net;

namespace ConflictsBot
{
    internal class ConflictsCheckerBot
    {
        public ConflictsCheckerBot(
            IRestApi restApi,
            BotConfiguration botConfig,
            FileStorage resolvedBranchesStorage,
            FileStorage readyToMergeBranchesStorage,
            string botName)
        {
            mRestApi = restApi;
            mBotConfig = botConfig;
            mResolvedBranchesStorage = resolvedBranchesStorage;
            mReadyToMergeBranchesStorage = readyToMergeBranchesStorage;
            mBotName = botName;
        }

        internal void LoadBranchesToProcess()
        {
            List<Branch> branches = FindQueries.FindResolvedBranches(
                mRestApi,
                mBotConfig.Repository,
                mBotConfig.BranchPrefix ?? string.Empty,
                mBotConfig.PlasticStatusAttrConfig.Name,
                mBotConfig.PlasticStatusAttrConfig.ResolvedValue);

            mResolvedBranchesStorage.Write(branches);
        }

        internal void OnAttributeChanged(string obj)
        {
            throw new NotImplementedException();
        }

        internal void ProcessBranches(object state)
        {
            while (true)
            {
                Branch branch;
                lock (mSyncLock)
                {
                    if (!mResolvedBranchesStorage.HasQueuedBranches())
                    {
                        Monitor.Wait(mSyncLock, 10000);
                        continue;
                    }

                    branch = mResolvedBranchesStorage.DequeueBranch();
                    branch.FullName = FindQueries.GetBranchName(mRestApi, branch.Repository, branch.Id);
                }

                mLog.InfoFormat(
                    "Checking if branch {0} has merge conflicts with {1} branch...",
                    branch.FullName, mBotConfig.TrunkBranch);

                if (!HasToTriggerTryMerge(
                    mRestApi, 
                    mBotConfig.IssueTrackerConfig, 
                    branch.GetShortName(), 
                    mBotConfig.BranchPrefix))
                {
                    mLog.InfoFormat("Branch {0} is not ready to trigger a try-merge. It will be queued again.", branch.FullName);

                    lock (mSyncLock)
                    {
                        mResolvedBranchesStorage.EnqueueBranch(branch);
                    }
                }

                BranchMerger.Result result = BranchMerger.Try(mRestApi, branch, mBotConfig);

                if (!result.HasManualConflicts)
                {
                    mLog.InfoFormat(
                        "Branch {0} has no manual conflicts with {1} at this repository state.", 
                        branch.FullName, mBotConfig.TrunkBranch);
                    
                    //NOTIFY

                    lock (mSyncLock)
                    {
                        mReadyToMergeBranchesStorage.EnqueueBranch(branch);
                    }
                    continue;
                }

                mLog.InfoFormat("Branch {0} has manual conflicts.", branch.FullName);              

                Thread.Sleep(5000);
            }
        }

        static bool HasToTriggerTryMerge(
            IRestApi restApi,
            BotConfiguration.IssueTracker issueTrackerConfig, 
            string branchShortName, 
            string branchPrefixToTrack)
        {
            string taskNumber = GetTaskNumber(branchShortName, branchPrefixToTrack);
            if (taskNumber == null)
                return false;

            if (issueTrackerConfig == null) //no issue tracker config -> just check the branch status attr
                return true;

            mLog.InfoFormat("Checking if issue tracker [{0}] is available...", issueTrackerConfig.PlugName);
            if (!restApi.IsIssueTrackerConnected(issueTrackerConfig.PlugName))
            {
                mLog.WarnFormat("Issue tracker [{0}] is NOT available...", issueTrackerConfig.PlugName);
                return false;
            }

            mLog.InfoFormat("Checking if task {0} is ready in the issue tracker [{1}].",
                taskNumber, issueTrackerConfig.PlugName);

            string status = restApi.GetIssueTrackerField(
                issueTrackerConfig.PlugName, 
                issueTrackerConfig.ProjectKey,
                taskNumber, 
                issueTrackerConfig.StatusField.Name);

            mLog.DebugFormat("Issue tracker status for task [{0}]: expected [{1}], was [{2}]",
                taskNumber, issueTrackerConfig.StatusField.ResolvedValue, status);

            return status.ToLowerInvariant().Trim().Equals(
                issueTrackerConfig.StatusField.ResolvedValue.ToLowerInvariant().Trim());
        }

        static string GetTaskNumber(
            string branchShortName,
            string branchPrefixToTrack)
        {            
            if (string.IsNullOrEmpty(branchPrefixToTrack))
                return branchShortName;

            if (branchShortName.StartsWith(branchPrefixToTrack, StringComparison.InvariantCultureIgnoreCase))
                return branchShortName.Substring(branchPrefixToTrack.Length);

            return null;
        }

        readonly object mSyncLock = new object();

        BotConfiguration mBotConfig;
        FileStorage mResolvedBranchesStorage;
        FileStorage mReadyToMergeBranchesStorage;
        string mBotName;
        IRestApi mRestApi;
        static readonly ILog mLog = LogManager.GetLogger(typeof(ConflictsCheckerBot));
    }
}