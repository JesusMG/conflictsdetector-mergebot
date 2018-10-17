using System;
using System.Collections.Generic;

namespace ConflictsBot
{
    public class MergeReport
    {
        public class Entry
        {
            public string Text { get; set; }
            public string Link { get; set; }
            public string Type { get; set; }
            public string Value { get; set; }
        }

        public DateTime Timestamp { get; set; }
        public string RepositoryId { get; set; }
        public int BranchId { get; set; }
        public List<Entry> Properties { get; set; }
    }

    internal static class MergeReporter
    {
        internal static MergeReport CreateReport(string repositoryId, int branchId)
        {
            MergeReport result = new MergeReport();
            result.Timestamp = DateTime.UtcNow;
            result.RepositoryId = repositoryId;
            result.BranchId = branchId;
            result.Properties = new List<MergeReport.Entry>();
            return result;
        }

        internal static void NotifyMerge(
            IRestApi restApi,
            string mergeBotName,
            string repository,
            string branchFullName,
            bool bHasManualMergeConflicts,
            string mergeMessage)
        {
            string repId;
            int branchId;

            bool bSuccessful = restApi.GetBranchIdData(repository, branchFullName, out repId, out branchId);

            if (!bSuccessful)
                return;

            MergeReport report = new MergeReport();
            report.Timestamp = DateTime.UtcNow;
            report.RepositoryId = repId;
            report.BranchId = branchId;
            report.Properties = new List<MergeReport.Entry>();

            MergeReport.Entry mergeProperty = new MergeReport.Entry();
            mergeProperty.Type = bHasManualMergeConflicts ? "merge_failed" : "merge_ok";
            mergeProperty.Value = mergeMessage;

            report.Properties.Add(mergeProperty);

            restApi.SendMergeReport(mergeBotName, report);
        }
    }
}