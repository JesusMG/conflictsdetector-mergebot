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

                if (!IsBranchTaskReady())
                {
                    mLog.InfoFormat("Branch {0} is not ready. It will be queued again.", branch.FullName);

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

        bool IsBranchTaskReady()
        {
            throw new NotImplementedException();
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