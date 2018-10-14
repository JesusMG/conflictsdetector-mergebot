using System;
using System.IO;
using System.Net;
using System.Net.Http;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConflictsBot
{
    public interface IRestApi
    {
        JArray Find(
            string repoName,
            string query,
            string queryDateFormat,
            string actionDescription,
            string[] fields);        
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

        static Uri GetFullUri(Uri baseUri, string partialUri)
        {
            return new Uri(baseUri, partialUri);
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

            static HttpWebRequest CreateWebRequest(
                Uri endpoint, HttpMethod httpMethod, string apiKey)
            {
                HttpWebRequest request = WebRequest.CreateHttp(endpoint);
                request.Method = httpMethod.Method;
                SetApiKeyAuth(request, apiKey);

                request.ContentLength = 0;

                return request;
            }

            static TRes GetResponse<TRes>(WebRequest request)
            {
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return JsonConvert.DeserializeObject<TRes>(reader.ReadToEnd());
                }
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

        static readonly ILog mLog = LogManager.GetLogger(typeof(RestApi));
    }

}