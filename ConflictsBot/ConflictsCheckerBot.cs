using System;
using System.Collections.Generic;
using System.Threading;
using log4net;
using Newtonsoft.Json;
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

            mResolvedBranchesStorage.Write(branches);
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

            if (!e.BranchFullName.Equals(mBotConfig.TrunkBranch))
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
                    Branch.GetShortName(branch.FullName),
                    mBotConfig.BranchPrefix))
                {
                    mLog.InfoFormat("Branch {0} is not ready to trigger a try-merge. It will be queued again.", branch.FullName);

                    lock (mSyncLock)
                    {
                        mResolvedBranchesStorage.EnqueueBranch(branch);
                    }
                }

                //TODO: Build MErge report and fill.

                BranchMerger.Result result = BranchMerger.Try(
                    mRestApi, branch.Repository, branch.FullName, mBotConfig.TrunkBranch);

                if (!result.HasManualConflicts)
                {
                    mLog.InfoFormat(
                        "Branch {0} has no manual conflicts with {1} at this repository state.",
                        branch.FullName, mBotConfig.TrunkBranch);

                    //TODO: NOTIFY

                    lock (mSyncLock)
                    {
                        mReadyToMergeBranchesStorage.EnqueueBranch(branch);
                    }
                    continue;
                }

                //TODO: Notify, and reopen

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