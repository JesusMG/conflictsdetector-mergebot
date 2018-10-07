namespace ConflictsBot
{
    internal class Branch
    {
        internal string Repository;
        internal string Id;
        internal string FullName;
        internal string Owner;
        internal string Comment;

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
    }
}