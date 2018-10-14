using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using log4net;
using Newtonsoft.Json;

namespace ConflictsBot
{
    internal class FileStorage
    {
        internal FileStorage(string filePath)
        {
            mStorageFilePath = filePath;
        }

        internal void Write(List<Branch> branches)
        {
            if (branches == null)
                return;

            try
            {
                using (StreamWriter file = new StreamWriter(mStorageFilePath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, branches);
                }
            }
            catch (Exception ex)
            {
                LogException("Error writing the queued branches to '{0}': {1}", ex, mStorageFilePath);
            }
        }

        internal bool HasQueuedBranches()
        {
            return GetQueuedBranches().Count > 0;
        }

        List<Branch> GetQueuedBranches()
        {
            List<Branch> branches = new List<Branch>();
            using (StreamReader file = new StreamReader(mStorageFilePath))
            {
                JsonSerializer serializer = new JsonSerializer();
                branches = (List<Branch>)serializer.Deserialize(file, typeof(List<Branch>));
            }
            return branches;
        }

        internal void EnqueueBranch(Branch branch)
        {
            if (Contains(branch.Repository, branch.Id))
                return;

            List<Branch> queuedBranches = GetQueuedBranches();

            queuedBranches.Add(branch);

            Write(queuedBranches);
        }

        internal Branch DequeueBranch()
        {
            List<Branch> queuedBranches = GetQueuedBranches();

            if (queuedBranches.Count == 0)
                return null;

            Branch dequeueBranch = queuedBranches[0];
            queuedBranches.RemoveAt(0);

            Write(queuedBranches);

            return dequeueBranch;
        }

        internal bool Contains(string repository, string branchId)
        {
            List<Branch> branches = GetQueuedBranches();
            return BranchFinder.IndexOf(branches, repository, branchId) > -1;
        }

        static void LogException(string message, Exception e, params string[] args)
        {
            mLog.ErrorFormat(message, args, e.Message);
            mLog.DebugFormat("StackTrace:{0}{1}", Environment.NewLine, e.StackTrace);
        }

        string mStorageFilePath;
        static readonly ILog mLog = LogManager.GetLogger(typeof(FileStorage));

        static class BranchFinder
        {
            internal static int IndexOf(
                List<Branch> branches, string repository, string branchId)
            {
                for (int i = 0; i < branches.Count; i++)
                {
                    if (!branches[i].Repository.Equals(repository))
                        continue;

                    if (!branches[i].Id.Equals(branchId))
                        continue;

                    return i;
                }

                return -1;
            }
        }
    }
}