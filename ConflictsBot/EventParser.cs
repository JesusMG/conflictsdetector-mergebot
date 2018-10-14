using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConflictsBot
{
    internal class BranchAttributeChangeEvent
    {
        public string Repository { get; set; }
        public string BranchId { get; set; }
        public string BranchFullName { get; set; }
        public string BranchOwner { get; set; }
        public string BranchComment { get; set; }
        public string AttributeName { get; set; }
        public string AttributeValue { get; set; }
    }

    internal class NewChangesetsEvent
    {
        public string Repository { get; set; }

        public string BranchFullName { get; set; }
    }

    internal static class EventParser
    {
        internal static BranchAttributeChangeEvent ParseBranchAttributeChangeEvent(string message)
        {
            string properties = GetPropertiesFromMessage(message);
            return JsonConvert.DeserializeObject<BranchAttributeChangeEvent>(properties);
        }

        internal static NewChangesetsEvent ParseNewChangesetsEvent(string message)
        {
            string properties = GetPropertiesFromMessage(message);
            return JsonConvert.DeserializeObject<NewChangesetsEvent>(properties);
        }

        static string GetPropertiesFromMessage(string message)
        {
            try
            {
                JObject obj = JObject.Parse(message);
                return obj.Value<object>("properties").ToString();
            }
            catch
            {
                // pending to add log
                return string.Empty;
            }
        }
    }
}