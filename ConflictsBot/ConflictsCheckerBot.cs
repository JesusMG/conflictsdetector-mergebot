using System;
using System.Collections.Generic;
using System.Threading;
using log4net;

namespace ConflictsBot
{
    internal class ConflictsCheckerBot
    {
        public ConflictsCheckerBot(
            RestApi restApi,
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

                mLog.InfoFormat("Processing branch {0} attribute change...", branch.FullName);
                ProcessBranch.Result result = ProcessBranch.TryProcessBranch(
                    mRestApi, branch, mBotConfig, mBotName);

                if (result == ProcessBranch.Result.Ok)
                {
                    mLog.InfoFormat("Branch {0} processing completed.", branch.FullName);
                    continue;
                }

                if (result == ProcessBranch.Result.Failed)
                {
                    mLog.InfoFormat("Branch {0} processing failed.", branch.FullName);
                    continue;
                }

                mLog.InfoFormat("Branch {0} is not ready. It will be queued again.", branch.FullName);

                lock (mSyncLock)
                {
                    mResolvedBranchesStorage.EnqueueBranch(branch);
                }

                Thread.Sleep(5000);
            }
        }

        readonly object mSyncLock = new object();

        BotConfiguration mBotConfig;
        FileStorage mResolvedBranchesStorage;
        FileStorage mReadyToMergeBranchesStorage;
        string mBotName;
        RestApi mRestApi;
        static readonly ILog mLog = LogManager.GetLogger(typeof(ConflictsCheckerBot));
    }
}