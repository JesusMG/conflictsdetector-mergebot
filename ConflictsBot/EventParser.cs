using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConflictsBot
{
    internal class BranchAttributeChangeEvent
    {
        [JsonProperty("repository")]
        public string Repository { get; set; }
        [JsonProperty("branchId")]
        public string BranchId { get; set; }
        [JsonProperty("branchFullName")]
        public string BranchFullName { get; set; }
        [JsonProperty("branchOwner")]
        public string BranchOwner { get; set; }
        [JsonProperty("branchComment")]
        public string BranchComment { get; set; }
        [JsonProperty("attributeName")]
        public string AttributeName { get; set; }
        [JsonProperty("attributeValue")]
        public string AttributeValue { get; set; }
    }

    internal class NewChangesetsEvent
    {
        [JsonProperty("repository")]
        public string Repository { get; set; }

        [JsonProperty("branch")]
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