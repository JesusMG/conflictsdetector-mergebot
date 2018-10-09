using System;
using System.Collections.Generic;

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

        internal void ProcessBranches(object state)
        {
            throw new NotImplementedException();
        }

        internal void OnAttributeChanged(string obj)
        {
            throw new NotImplementedException();
        }

        string mRestApiUrl;
        BotConfiguration mBotConfig;
        string mResolvedBranchesQueueFile;
        string mBotName;

        RestApi mRestApi;
    }
}