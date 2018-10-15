using System;

namespace ConflictsBot
{
    public class Branch
    {
        public string Repository;
        public string Id;
        public string FullName;
        public string Owner;
        public string Comment;

        public Branch() { }

        internal Branch(
            string repository,
            string id,
            string fullName,
            string owner,
            string comment)
        {
            Repository = repository;
            Id = id;
            FullName = fullName;
            Owner = owner;
            Comment = comment;
        }

        internal static string GetShortName(string fullName)
        {
            int separatorIndex = fullName.LastIndexOf('/');

            if (separatorIndex == -1)
                return fullName;

            return fullName.Substring(separatorIndex + 1);
        }

        internal static string NormalizeFullName(string branchFullName)
        {
            branchFullName = branchFullName.Trim();
            if (branchFullName.StartsWith("br:"))
                branchFullName = branchFullName.Remove("br:".Length);

            if (branchFullName.StartsWith("/"))
                return branchFullName;

            return "/" + branchFullName;
        }
    }
}