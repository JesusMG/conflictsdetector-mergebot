using System;
using System.Collections.Generic;
using System.Threading;
using log4net;

namespace ConflictsBot
{
    internal class ConflictsCheckerBot
    {
        public ConflictsCheckerBot(
            string restApiUrl,
            BotConfiguration botConfig, 
            string resolvedBranchesQueueFile, 
            string botName)
        {
            mRestApiUrl = restApiUrl;
            mBotConfig = botConfig;
            mResolvedBranchesQueueFile = resolvedBranchesQueueFile;
            mBotName = botName;

            mRestApi = new RestApi(restApiUrl, botConfig.PlasticBotUserToken);
        }

        internal void LoadBranchesToProcess()
        {
            List<Branch> branches = FindQueries.FindResolvedBranches(
                mRestApi,
                mBotConfig.Repository,
                mBotConfig.BranchPrefix ?? string.Empty,
                mBotConfig.PlasticStatusAttrConfig.Name,
                mBotConfig.PlasticStatusAttrConfig.ResolvedValue);

            FileStorage.ResolvedQueue.Write(branches, mResolvedBranchesQueueFile);
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
                    if (!FileStorage.ResolvedQueue.HasQueuedBranches(mResolvedBranchesQueueFile))
                    {
                        Monitor.Wait(mSyncLock, 10000);
                        continue;
                    }

                    branch = FileStorage.ResolvedQueue.DequeueBranch(mResolvedBranchesQueueFile);
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
                    FileStorage.ResolvedQueue.EnqueueBranch(branch, mResolvedBranchesQueueFile);
                }

                Thread.Sleep(5000);
            }
        }

        readonly object mSyncLock = new object();

        string mRestApiUrl;
        BotConfiguration mBotConfig;
        string mResolvedBranchesQueueFile;
        string mBotName;
        RestApi mRestApi;
        static readonly ILog mLog = LogManager.GetLogger(typeof(ConflictsCheckerBot));
    }
}