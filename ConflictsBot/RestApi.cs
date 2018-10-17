using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace ConflictsBot
{
    public partial interface IRestApi
    {
        JArray Find(
            string repoName,
            string query,
            string queryDateFormat,
            string actionDescription,
            string[] fields);

        void UpdateBranchAttribute(string repository, string branch, string attrName, string attrValue);

        RestApi.MergeToResponse MergeBranchToShelve(
		    string repository, 
			string fullName, 
			string trunkBranch);

        bool IsIssueTrackerConnected(string plugName);

        string GetIssueTrackerField(
			string plugName, 
			string projectKey, 
			string taskNumber, 
			string name);

        string UpdateIssueTrackerField(string plugName, string projectKey, string taskNumber, string fieldName, string fieldValue);

		void Notify(string plugName, string message, List<string> recipients);

        void SendMergeReport(string mergeBotName, MergeReport report);

        JObject GetUserProfile(string user);

        void DeleteShelve(
            IRestApi restApi, 
            string repository, 
            int shelveId);
        bool GetBranchIdData(string repository, string branchFullName, out string repId, out int branchId);

    }

    public class RestApi : IRestApi
    {
        internal RestApi(string restApiUrl, string plasticBotUserToken)
        {
            mBaseUri = new Uri(restApiUrl);
            mPlasticBotUserToken = plasticBotUserToken;
        }

        public JArray Find(
            string repoName,
            string query,
            string queryDateFormat,
            string actionDescription,
            string[] fields)
        {
            string fieldsQuery = string.Empty;
            if (fields != null && fields.Length > 0)
                fieldsQuery = string.Join(",", fields);

            Uri endpoint = ApiUris.GetFullUri(
                mBaseUri, ApiEndpoints.Find, repoName, query, queryDateFormat, fieldsQuery);

            return Internal.MakeApiRequest<JArray>(
                endpoint, HttpMethod.Get, actionDescription, mPlasticBotUserToken);
        }

        public bool IsIssueTrackerConnected(string plugName)
        {
            Uri endpoint = ApiUris.GetFullUri(
                    mBaseUri, ApiEndpoints.Issues.IsConnected,
                    plugName);

            string actionDescription = string.Format("test connection to '{0}'", plugName);

            SingleResponse response = Internal.MakeApiRequest<SingleResponse>(
                endpoint, HttpMethod.Get, actionDescription, mPlasticBotUserToken);
                
            bool flag;
            if (Boolean.TryParse(response.Value, out flag))
                return flag;

            return false;
        }

        public string UpdateIssueTrackerField(
            string plugName, 
            string projectKey, 
            string taskNumber, 
            string fieldName, 
            string fieldValue)
        {
            SetIssueFieldRequest request = new SetIssueFieldRequest()
            {
                NewValue = fieldValue
            };

            Uri endpoint = ApiUris.GetFullUri(
                mBaseUri, 
                ApiEndpoints.Issues.SetIssueField,
                plugName, 
                projectKey, 
                taskNumber, 
                fieldName);

            string actionDescription = string.Format(
                "set field '{0}' of issue {1}-{2} in {3} to value '{4}'",
                fieldName, projectKey, taskNumber, plugName, request.NewValue);

            return Internal.MakeApiRequest<SetIssueFieldRequest, SingleResponse>(
                endpoint, HttpMethod.Put, request, actionDescription, mPlasticBotUserToken).Value;
        }

        public void UpdateBranchAttribute(string repository, string branchFullName, string attrName, string attrValue)
        {
            string actionDescription = string.Format(
                "update attribute {0}={1} of branch {2}" , attrName, attrValue, branchFullName);

            ChangeAttributeRequest request = new ChangeAttributeRequest()
            {
                TargetType = ChangeAttributeRequest.AttributeTargetType.Branch,
                TargetName = branchFullName,
                Value = attrValue
            };

            Uri endpoint = ApiUris.GetFullUri(
                mBaseUri, ApiEndpoints.ChangeAttribute,
                repository, attrName);

            Internal.MakeApiRequest<ChangeAttributeRequest>(
                endpoint, HttpMethod.Put, request, actionDescription, mPlasticBotUserToken);
        }

        public JObject GetUserProfile(string user)
        {
            string actionDescription = string.Format("get profile of user '{0}'", user);

            Uri endpoint = ApiUris.GetFullUri(
                    mBaseUri, ApiEndpoints.Users.GetUserProfile, user);

            return Internal.MakeApiRequest<JObject>(
                endpoint, HttpMethod.Get, actionDescription, mPlasticBotUserToken);
        }

        public void Notify(string notifierPlugName, string message, List<string> recipients)
        {
            if (recipients == null || recipients.Count == 0)
                return;

            string actionDescription = string.Format("nofify message to '{0}'", string.Join(" ", recipients));

            NotifyMessageRequest request = new NotifyMessageRequest()
            {
                Message = message,
                Recipients = recipients
            };

            Uri endpoint = ApiUris.GetFullUri(
                    mBaseUri, ApiEndpoints.Notify.NotifyMessage, notifierPlugName);

            Internal.MakeApiRequest<NotifyMessageRequest>(
                endpoint, HttpMethod.Post, request, actionDescription, mPlasticBotUserToken);
        }

        public void SendMergeReport(string mergeBotName, MergeReport report)
        {
            Uri endpoint = ApiUris.GetFullUri(
                mBaseUri, 
                ApiEndpoints.MergeReports.ReportMerge,
                mergeBotName);

            string actionDescription = string.Format(
                "upload merge report of br:{0} (repo ID: {1})",
                report.BranchId,
                report.RepositoryId);

            Internal.MakeApiRequest<MergeReport>(
                endpoint, HttpMethod.Put, report, actionDescription, mPlasticBotUserToken);
        }

        public string GetIssueTrackerField(
            string plugName, 
            string projectKey, 
            string taskNumber, 
            string fieldName)
        {
            Uri endpoint = ApiUris.GetFullUri(
                mBaseUri, 
                ApiEndpoints.Issues.GetIssueField,
                plugName, 
                projectKey, 
                taskNumber, 
                fieldName);

            string actionDescription = string.Format(
                "get field '{0}' of issue {1}-{2} in {3}",
                fieldName, projectKey, taskNumber, plugName);

            SingleResponse response = Internal.MakeApiRequest<SingleResponse>(
                endpoint, HttpMethod.Get, actionDescription, mPlasticBotUserToken);
                
            return response.Value;
        }

        public RestApi.MergeToResponse MergeBranchToShelve(
            string repository, 
            string fullName, 
            string trunkBranch)
        {
            MergeToRequest request = new MergeToRequest()
            {
                SourceType = MergeToRequest.MergeToSourceType.Branch,
                Source = fullName,
                Destination = trunkBranch,
                Comment = string.Empty,
                CreateShelve = true,
                EnsureNoDstChanges = false
            };

            Uri endpoint = ApiUris.GetFullUri(
                mBaseUri, ApiEndpoints.MergeTo, repository);

            string actionDescription = string.Format(
                "merge from {0} '{1}' to '{2}'",
                request.SourceType,
                request.Source,
                request.Destination);

            return Internal.MakeApiRequest<MergeToRequest, MergeToResponse>(
                endpoint, HttpMethod.Post, request, actionDescription, mPlasticBotUserToken);
        }

        public void DeleteShelve(IRestApi restApi, string repository, int shelveId)
        {
            Uri endpoint = ApiUris.GetFullUri(
                mBaseUri, ApiEndpoints.DeleteShelve,
                repository, shelveId.ToString());

            string actionDescription = string.Format(
                "delete shelve sh:{0}@{1}", shelveId, repository);

            Internal.MakeApiRequest<SingleResponse>(
                endpoint, HttpMethod.Delete, actionDescription, mPlasticBotUserToken);
        }

        public bool GetBranchIdData(string repository, string branchFullName, out string repId, out int branchId)
        {
            repId = null;
            branchId = -1;

            if (branchFullName.StartsWith("/"))
                branchFullName = branchFullName.Substring(1);

            Uri endpoint = ApiUris.GetFullUri(
                mBaseUri, ApiEndpoints.GetBranch, repository, branchFullName);

            string actionDescription = string.Format(
                "get info of branch br:{0}@{1}", branchFullName, repository);

            BranchResponse response = Internal.MakeApiRequest<BranchResponse>(
                endpoint, HttpMethod.Get, actionDescription, mPlasticBotUserToken);

            if (response == null)
                return false;
            
            repId = response.RepositoryId;
            branchId = response.Id;

            return true;
        }

        readonly Uri mBaseUri;
        string mPlasticBotUserToken;

        internal static class ApiUris
        {
            internal static Uri GetFullUri(Uri baseUri, string partialUri)
            {
                return new Uri(baseUri, partialUri);
            }

            internal static Uri GetFullUri(Uri baseUri, string partialUri, params string[] args)
            {
                string[] requestParams = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                    requestParams[i] = WebUtility.UrlEncode(args[i]);

                string endpoint = string.Format(partialUri, requestParams);
                return new Uri(baseUri, endpoint);
            }            
        }

        static class ApiEndpoints
        {
            internal const string GetBranch = "/api/v1/repos/{0}/branches/{1}";
            internal const string GetChangeset = "/api/v1/repos/{0}/changesets/{1}";
            internal const string GetAttribute = "/api/v1/repos/{0}/attributes/{1}/{2}/{3}";
            internal const string ChangeAttribute = "/api/v1/repos/{0}/attributes/{1}";
            internal const string MergeTo = "/api/v1/repos/{0}/mergeto";
            internal const string DeleteShelve = "/api/v1/repos/{0}/shelve/{1}";
            internal const string Find = "/api/v1/repos/{0}/find?query={1}&queryDateFormat={2}&fields={3}";

            internal static class Users
            {
                internal const string GetUserProfile = "/api/v1/users/{0}/profile";
            }

            internal static class MergeReports
            {
                internal const string ReportMerge = "/api/v1/mergereports/{0}";
            }

            internal static class Issues
            {
                internal const string IsConnected = "/api/v1/issues/{0}/checkconnection";
                internal const string GetIssueField = "/api/v1/issues/{0}/{1}/{2}/{3}";
                internal const string SetIssueField = "/api/v1/issues/{0}/{1}/{2}/{3}";
            }

            internal static class Notify
            {
                internal const string NotifyMessage = "/api/v1/notify/{0}";
            }
        }

        static class Internal
        {
            internal static TRes MakeApiRequest<TRes>(
                    Uri endpoint, HttpMethod httpMethod, string actionDescription, string apiKey)
            {
                try
                {
                    HttpWebRequest request = CreateWebRequest(
                        endpoint, httpMethod, apiKey);
                    return GetResponse<TRes>(request);
                }
                catch (WebException ex)
                {
                    throw WebServiceException.AdaptException(ex, actionDescription, endpoint);
                }
                catch (Exception ex)
                {
                    ExceptionLogger.LogException(
                        actionDescription,
                        ex.Message,
                        ex.StackTrace,
                        endpoint,
                        HttpStatusCode.OK);
                    throw;
                }
            }

            internal static TRes MakeApiRequest<TReq, TRes>(
                Uri endpoint, HttpMethod httpMethod, TReq body, string actionDescription, string apiKey)
            {
                try
                {
                    HttpWebRequest request = CreateWebRequest<TReq>(
                        endpoint, httpMethod, body, apiKey);

                    return GetResponse<TRes>(request);
                }
                catch (WebException ex)
                {
                    throw WebServiceException.AdaptException(ex, actionDescription, endpoint);
                }
                catch (Exception ex)
                {
                    ExceptionLogger.LogException(actionDescription, ex.Message, ex.StackTrace, endpoint, HttpStatusCode.OK);
                    throw;
                }
            }

            internal static void MakeApiRequest<TReq>(
                Uri endpoint, HttpMethod httpMethod, TReq body, string actionDescription, string apiKey)
            {
                try
                {
                    HttpWebRequest request = CreateWebRequest<TReq>(
                        endpoint, httpMethod, body, apiKey);

                    GetResponse(request);
                }
                catch (WebException ex)
                {
                    throw WebServiceException.AdaptException(ex, actionDescription, endpoint);
                }
                catch (Exception ex)
                {
                    ExceptionLogger.LogException(
                        actionDescription, 
                        ex.Message, 
                        ex.StackTrace, 
                        endpoint,
                        HttpStatusCode.OK);
                    throw;
                }
            }

            static HttpWebRequest CreateWebRequest(
                Uri endpoint, HttpMethod httpMethod, string apiKey)
            {
                HttpWebRequest request = WebRequest.CreateHttp(endpoint);
                request.Method = httpMethod.Method;
                SetApiKeyAuth(request, apiKey);

                request.ContentLength = 0;

                return request;
            }

            static HttpWebRequest CreateWebRequest<TReq>(Uri endpoint, HttpMethod httpMethod, TReq body, string apiKey)
            {
                HttpWebRequest request = WebRequest.CreateHttp(endpoint);
                request.Method = httpMethod.Method;
                request.ContentType = "application/json";
                SetApiKeyAuth(request, apiKey);

                WriteBody(request, body);

                return request;
            }

            static void WriteBody(WebRequest request, object body)
            {
                using (Stream st = request.GetRequestStream())
                using (StreamWriter writer = new StreamWriter(st))
                {
                    writer.Write(JsonConvert.SerializeObject(body));
                }
            }

            static TRes GetResponse<TRes>(WebRequest request)
            {
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return JsonConvert.DeserializeObject<TRes>(reader.ReadToEnd());
                }
            }

            static void GetResponse(WebRequest request)
            {
                using (WebResponse response = request.GetResponse()){}
            }

            static void SetApiKeyAuth(HttpWebRequest request, string apiKey)
            {
                request.Headers["Authorization"] = "ApiKey " + apiKey;
            }
        }

        static class WebServiceException
        {
            internal static Exception AdaptException(
                WebException ex, string actionDescription, Uri endpoint)
            {
                string message = GetExceptionMessage(ex, endpoint);
                ExceptionLogger.LogException(
                    actionDescription,
                    message,
                    ex.StackTrace,
                    endpoint,
                    GetStatusCode(ex.Response));

                return new Exception(message);
            }

            static HttpStatusCode GetStatusCode(WebResponse exceptionResponse)
            {
                HttpWebResponse httpResponse = exceptionResponse as HttpWebResponse;
                return httpResponse != null ? httpResponse.StatusCode : HttpStatusCode.OK;
            }

            static string GetExceptionMessage(WebException ex, Uri endpoint)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response == null)
                    return ex.Message;

                try
                {
                    return ReadErrorMessageFromResponse(response);
                }
                catch (Exception e)
                {
                    mLog.ErrorFormat("Unable to read the error response: {0}", e.Message);
                    mLog.DebugFormat("Endpoint: {0}", endpoint);
                    mLog.DebugFormat("Stack trace:{0}{1}", Environment.NewLine, e.StackTrace);
                    return ex.Message;
                }
            }

            static string ReadErrorMessageFromResponse(HttpWebResponse response)
            {
                using (StreamReader resultStream =
                    new StreamReader(response.GetResponseStream()))
                {
                    JObject jObj = JsonConvert.DeserializeObject<JObject>(
                        resultStream.ReadToEnd());

                    return jObj.Value<JObject>("error").Value<string>("message");
                }
            }
        }

        static class ExceptionLogger
        {
            internal static void LogException(
                string actionDescription,
                string errorMessage,
                string stackTrace,
                Uri endpoint,
                HttpStatusCode statusCode)
            {
                mLog.ErrorFormat(
                    "Unable to {0}. The server returned: {1}. {2}",
                    actionDescription,
                    errorMessage,
                    GetStatusCodeDetails(statusCode));

                mLog.DebugFormat("Endpoint URI: {0}", endpoint);
                mLog.DebugFormat("Stack trace:{0}{1}", Environment.NewLine, stackTrace);
            }

            static string GetStatusCodeDetails(HttpStatusCode statusCode)
            {
                switch (statusCode)
                {
                    case HttpStatusCode.Unauthorized:
                        return "Please check that the User API Key assigned to the bot is " +
                            "correct and the associated user has enough permissions.";
                    case HttpStatusCode.InternalServerError:
                        return "Please check the Plastic SCM Server log.";
                    case HttpStatusCode.NotFound:
                        return "The requested element doesn't exist.";
                    case HttpStatusCode.BadRequest:
                        return "The server couldn't understand the request data.";
                    default:
                        return string.Empty;
                }
            }
        }

        class SingleResponse
        {
               public string Value { get; set; }
        }

        public class MergeToResponse
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public MergeToResultStatus Status { get; set; }
            public string Message { get; set; }
            public int ChangesetNumber { get; set; }
    
            public enum MergeToResultStatus : byte
            {
                OK = 0,
                AncestorNotFound = 1,
                MergeNotNeeded = 2,
                Conflicts = 3,
                DestinationChanges = 4,
                Error = 5,
                MultipleHeads = 6
            }
       }

       public class MergeToRequest
        {
            [JsonConverter(typeof(StringEnumConverter))]
            public MergeToSourceType SourceType { get; set; }
            public string Source { get; set; }
            public string Destination { get; set; }
            public string Comment { get; set; }
            public bool CreateShelve { get; set; }
            public bool EnsureNoDstChanges { get; set; }
        

            public enum MergeToSourceType : byte
            {
                Branch = 0,
                Shelve = 1,
                Label = 2,
                Changeset = 3
            }
        }

        public class ChangeAttributeRequest
        {
            public string TargetName { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public AttributeTargetType TargetType { get; set; }

            public string Value { get; set; }

            public enum AttributeTargetType : byte
            {
                Branch = 0,
                Label = 1,
                Changeset = 2
            }
        }

        class NotifyMessageRequest
        {
            public string Message { get; set; }
            public List<string> Recipients { get; set; }
        }

        class SetIssueFieldRequest
        {
            public string NewValue { get; set; }
        }

        class BranchResponse
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string RepositoryId { get; set; }
            public int HeadChangeset { get; set; }
            public DateTime Date { get; set; }
            public string Owner { get; set; }
            public string Comment { get; set; }
            public string Type { get; set; }
        }

        static readonly ILog mLog = LogManager.GetLogger(typeof(RestApi));
    }

}