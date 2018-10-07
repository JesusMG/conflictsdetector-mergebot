using System;
using System.Collections.Generic;

namespace ConflictsBot
{
    internal class ConflictsCheckerBot
    {
        public ConflictsCheckerBot(
            string restApiUrl,
            BotConfiguration botConfig, 
            string branchesToProcessFile, 
            string botName)
        {
            mRestApiUrl = restApiUrl;
            mBotConfig = botConfig;
            mBranchesToProcessFile = branchesToProcessFile;
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

            BranchesQueueStorage.WriteQueuedBranches(branches, mBranchesToProcessFile);
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
        string mBranchesToProcessFile;
        string mBotName;

        RestApi mRestApi;
    }
}