using System;

namespace ConflictsBot
{
    internal class ConflictsCheckerBot
    {
        string mRestApiUrl;
        BotConfiguration mBotConfig;
        string mBranchesToProcessFile;
        string mBotName;

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
        }

        internal void LoadBranchesToProcess()
        {
            throw new NotImplementedException();
        }

        internal void ProcessBranches(object state)
        {
            throw new NotImplementedException();
        }

        internal void OnAttributeChanged(string obj)
        {
            throw new NotImplementedException();
        }
    }
}