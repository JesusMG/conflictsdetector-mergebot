using System;
using System.Collections.Generic;
using System.Threading;

using log4net;

using Newtonsoft.Json.Linq;

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

            List<Branch> alreadyTracked = new List<Branch>();
            alreadyTracked.AddRange(mResolvedBranchesStorage.GetQueuedBranches());
            alreadyTracked.AddRange(mReadyToMergeBranchesStorage.GetQueuedBranches());

            FilterAlreadyTrackedBranches(branches, alreadyTracked);

            mResolvedBranchesStorage.Write(branches);
        }

        void FilterAlreadyTrackedBranches(
            List<Branch> branchesToFilter, 
            List<Branch> alreadyTrackedBranches)
        {
            if (alreadyTrackedBranches.Count == 0)
                return;

            for (int i = branchesToFilter.Count - 1; i >= 0; i--)
            {
                if (BranchFinder.IndexOf(alreadyTrackedBranches,
                    branchesToFilter[i].Repository, branchesToFilter[i].Id) == -1)
                {
                    continue;
                }

                branchesToFilter.RemoveAt(i);
            }
        }

        internal void OnEventReceived(string message)
        {
            mLog.Debug(message);

            if (IsBranchAttributeChangedEvent(message))
            {
                ProcessBranchAttributeChangedEvent(message);
                return;
            }

            if (IsNewChangesetsEvent(message))
            {
                ProcessNewChangesetsEvent(message);
                return;
            }
        }

        bool IsBranchAttributeChangedEvent(string message)
        {
            return GetEventTypeFromMessage(message).Equals(
                WebSocketClient.BRANCH_ATTRIBUTE_CHANGED_TRIGGER_TYPE);
        }

        bool IsNewChangesetsEvent(string message)
        {
            return GetEventTypeFromMessage(message).Equals(
                WebSocketClient.NEW_CHANGESETS_CHANGED_TRIGGER_TYPE);
        }

        static string GetEventTypeFromMessage(string message)
        {
            try
            {
                JObject obj = JObject.Parse(message);
                return obj.Value<string>("event").ToString();
            }
            catch
            {
                mLog.ErrorFormat("Unable to parse incoming event: {0}", message);
                return string.Empty;
            }
        }

        void ProcessBranchAttributeChangedEvent(string message)
        {
            BranchAttributeChangeEvent e = EventParser.ParseBranchAttributeChangeEvent(message);

            if (!IsEventOfValidTrackedBranch(e, mBotConfig))
                return;

            if (!AreEqualIgnoreCase(e.AttributeName, mBotConfig.PlasticStatusAttrConfig.Name))
                return;

            lock (mSyncLock)
            {
                if (AreEqualIgnoreCase(e.AttributeValue, mBotConfig.PlasticStatusAttrConfig.MergedValue))
                {
                    if (!mReadyToMergeBranchesStorage.Contains(e.Repository, e.BranchId))
                        return;

                    mReadyToMergeBranchesStorage.RemoveBranch(e.Repository, e.BranchId);
                }

                if (AreEqualIgnoreCase(e.AttributeValue, mBotConfig.PlasticStatusAttrConfig.ResolvedValue))
                {
                    if(mResolvedBranchesStorage.Contains(e.Repository, e.BranchId))
                        return;
                    
                    Branch branch = new Branch(e.Repository, e.BranchId, e.BranchFullName, e.BranchOwner, e.BranchComment);
                    mResolvedBranchesStorage.EnqueueBranch(branch);
                    return;
                }

                if(!mResolvedBranchesStorage.Contains(e.Repository, e.BranchId))
                    return;

                mResolvedBranchesStorage.RemoveBranch(e.Repository, e.BranchId);
            }
        }

        bool AreEqualIgnoreCase(string incomingValue, string expectedValue)
        {
             return incomingValue.Equals(
                 expectedValue, StringComparison.InvariantCultureIgnoreCase);
        }

        bool IsEventOfValidTrackedBranch(BranchAttributeChangeEvent e, BotConfiguration botConfig)
        {
            if (!e.Repository.Equals(botConfig.Repository, StringComparison.InvariantCultureIgnoreCase))
                return false;

            if (string.IsNullOrEmpty(botConfig.BranchPrefix))
                return true;

            string branchName = Branch.GetShortName(e.BranchFullName);

            return branchName.StartsWith(botConfig.BranchPrefix,
                StringComparison.InvariantCultureIgnoreCase);
        }

        void ProcessNewChangesetsEvent(string message)
        {
            NewChangesetsEvent e = EventParser.ParseNewChangesetsEvent(message);

            if (!e.Repository.Equals(mBotConfig.Repository, StringComparison.InvariantCultureIgnoreCase))
                return;

            if (!IsTrackedTrunkBranch(e.BranchFullName, mBotConfig.TrunkBranch))
                return;
            
            lock (mSyncLock)
            {
                while (mReadyToMergeBranchesStorage.HasQueuedBranches())
                {
                    mResolvedBranchesStorage.EnqueueBranch(
                        mReadyToMergeBranchesStorage.DequeueBranch());
                }                
            }
        }

        bool IsTrackedTrunkBranch(string branchFullName, string trunkBranch)
        {
            return Branch.NormalizeFullName(branchFullName).Equals(
                   Branch.NormalizeFullName(trunkBranch));
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
                
                string taskNumber = GetTaskNumber(
                    Branch.GetShortName(branch.FullName), mBotConfig.BranchPrefix);

                if (string.IsNullOrEmpty(taskNumber))
                {
                    mLog.WarnFormat("Unable to calculate the task number of branch {0}. Tracked branch prefix:{1}",
                        branch.FullName, mBotConfig.BranchPrefix);

                    continue;
                }

                if (!HasToTriggerTryMerge(
                    mRestApi,
                    taskNumber,
                    mBotConfig.IssueTrackerConfig))
                {
                    mLog.InfoFormat("Branch {0} is not ready to trigger a try-merge. It will be queued again.", branch.FullName);

                    lock (mSyncLock)
                    {
                        mResolvedBranchesStorage.EnqueueBranch(branch);
                    }
                }

                BranchMerger.Result result = BranchMerger.Try(
                    mRestApi, branch.Repository, branch.FullName, mBotConfig.TrunkBranch);

                if (result == null) //branch already merged!
                    continue;
                
                MergeReporter.NotifyMerge(
                    mRestApi, 
                    mBotName, 
                    branch.Repository, 
                    branch.FullName, 
                    result.HasManualConflicts, 
                    result.Message);
                
                string notifyMessage = string.Empty;

                if (!result.HasManualConflicts)
                {
                    mLog.InfoFormat(
                        "Branch {0} has no manual conflicts with {1} at this repository state.",
                        branch.FullName, mBotConfig.TrunkBranch);

                    notifyMessage = string.Format(
                        "Branch {0} has no manual conflicts with branch {1} and is able to be merged so far.",
                        branch.FullName, mBotConfig.TrunkBranch);
                    
                    Notifier.Notify(mRestApi, branch.Owner, notifyMessage, mBotConfig.NotifierConfig, true);

                    lock (mSyncLock)
                    {
                        mReadyToMergeBranchesStorage.EnqueueBranch(branch);
                    }
                    continue;
                }

                mLog.InfoFormat(
                    "Branch {0} has manual conflicts with branch {1}.", 
                    branch.FullName, mBotConfig.TrunkBranch);
                
                string extraIssueTrackerMessage = mBotConfig.IssueTrackerConfig == null ?
                    string.Empty :
                    string.Format(
                        "and the {0} plug's issue tracker field of {1} {2} to {3}",
                        mBotConfig.IssueTrackerConfig.PlugName,
                        mBotConfig.IssueTrackerConfig.ProjectKey,
                        taskNumber,
                        mBotConfig.IssueTrackerConfig.StatusField.ResolvedValue);

                notifyMessage = string.Format(
                    "Branch {0} has manual conflicts with branch {1} and cannot be merged. " + 
                    Environment.NewLine + Environment.NewLine +
                    "Please run a merge from branch {1} to branch {0} in your plastic workspace " +
                    "and resolve these manual conflicts. " + 
                    Environment.NewLine +
                    "Then, enqueue the branch {0} again by setting the {2} attribute of the {0} branch to {3} {4}", 
                    branch.FullName, 
                    mBotConfig.TrunkBranch, 
                    mBotConfig.PlasticStatusAttrConfig.Name, 
                    mBotConfig.PlasticStatusAttrConfig.ResolvedValue,
                    extraIssueTrackerMessage);

                Notifier.Notify(mRestApi, branch.Owner, notifyMessage, mBotConfig.NotifierConfig, false);

                StatusUpdater.UpdateBranchAttribute(
                    mRestApi, 
                    branch.Repository, 
                    branch.FullName, 
                    mBotConfig.PlasticStatusAttrConfig.Name, 
                    mBotConfig.PlasticStatusAttrConfig.FailedValue);

                if (mBotConfig.IssueTrackerConfig != null)
                {
                    StatusUpdater.UpdateIssueTrackerField(
                        mRestApi,
                        mBotConfig.IssueTrackerConfig.PlugName,
                        mBotConfig.IssueTrackerConfig.ProjectKey,
                        taskNumber,
                        mBotConfig.IssueTrackerConfig.StatusField.Name,
                        mBotConfig.IssueTrackerConfig.StatusField.FailedValue);
                }
            }
        }

        static bool HasToTriggerTryMerge(
            IRestApi restApi,
            string taskNumber,
            BotConfiguration.IssueTracker issueTrackerConfig)
        {
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