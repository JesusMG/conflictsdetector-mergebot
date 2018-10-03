using System;
using System.Threading;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json.Linq;

namespace ConflictsBot
{
    class WebSocketClient
    {
        internal WebSocketClient(
            string serverUrl,
            string name,
            string apikey,
            Action<string> processMessage)
        {
            mName = name;
            mApiKey = apikey;

            mCancelToken = mCancelTokenSource.Token;

            mUri = new Uri(serverUrl);

            mProcessMessage = processMessage;

            mOnClose = OnClose;
        }

        internal void ConnectWithRetries()
        {
            if (mbIsTryingConnection)
                return;

            mbIsTryingConnection = true;
            try
            {
                while (true)
                {
                    if (ConnectAsync().Result)
                        return;

                    System.Threading.Thread.Sleep(5000);
                }
            }
            finally
            {
                mbIsTryingConnection = false;
            }
        }
		
        async Task<bool> ConnectAsync()
        {
            if (mWebSocket != null)
                mWebSocket.Dispose();

            mWebSocket = new ClientWebSocket();
            mWebSocket.Options.RemoteCertificateValidationCallback += CertificateValidation;
            mWebSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

            await mWebSocket.ConnectAsync(mUri, mCancelToken);

            SendMessage(BuildLoginMessage(mApiKey));
            SendMessage(BuildRegisterTriggerMessage(BRANCH_ATTRIBUTE_CHANGED_TRIGGER_TYPE));

            mLog.InfoFormat("ConflictsBot [{0}] connected!", mName);

            Console.WriteLine("ConflictsBot [{0}] connected!", mName);

            StartListen();

            return mWebSocket.State == WebSocketState.Open;
        }

        static string BuildLoginMessage(string token)
        {
            JObject obj = new JObject(
                new JProperty("action", "login"),
                new JProperty("key", token));

            return obj.ToString();
        }

        static string BuildRegisterTriggerMessage(params string[] triggers)
        {
            JObject obj = new JObject(
                new JProperty("action", "register"),
                new JProperty("type", "trigger"),
                new JProperty("eventlist", new JArray(triggers)));

            return obj.ToString();
        }

        async void StartListen()
        {
            var buffer = new byte[ReceiveChunkSize];

            try
            {
                while (mWebSocket.State == WebSocketState.Open)
                {
                    var stringResult = new StringBuilder();


                    WebSocketReceiveResult result;
                    do
                    {
                        result = await mWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), mCancelToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await
                                mWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                            CallOnClose();
                        }
                        else
                        {
                            var str = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            stringResult.Append(str);
                        }

                    } while (!result.EndOfMessage);

                    CallOnMessage(stringResult);

                }
            }
            catch (Exception e)
            {
                mLog.ErrorFormat("StartListen failed: {0}", e.Message);
                CallOnClose();                
            }
        }

        void CallOnMessage(StringBuilder stringResult)
        {
            if (mProcessMessage == null)
                return;

            RunInTask(() => mProcessMessage(stringResult.ToString()));
        }

        void CallOnClose()
        {
            if (mOnClose == null)
                return;

            mOnClose();
        }

        static void RunInTask(Action action)
        {
            Task.Factory.StartNew(action);
        }

        void SendMessage(string message)
        {
            SendMessageAsync(message);
        }

        async void SendMessageAsync(string message)
        {
            if (mWebSocket.State != WebSocketState.Open)
            {
                throw new Exception("Connection is not open.");
            }

            var messageBuffer = Encoding.UTF8.GetBytes(message);
            var messagesCount = (int)Math.Ceiling((double)messageBuffer.Length / SendChunkSize);

            for (var i = 0; i < messagesCount; i++)
            {
                var offset = (SendChunkSize * i);
                var count = SendChunkSize;
                var lastMessage = ((i + 1) == messagesCount);

                if ((count * (i + 1)) > messageBuffer.Length)
                {
                    count = messageBuffer.Length - offset;
                }

                await mWebSocket.SendAsync(
                    new ArraySegment<byte>(messageBuffer, offset, count), 
                    WebSocketMessageType.Text, 
                    lastMessage, 
                    mCancelToken);
            }
        }

        static bool CertificateValidation(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        void OnClose()
        {
            mLog.InfoFormat("OnClose was called!");

            //await mWebSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, string.Empty, mCancelToken);

            ConnectWithRetries();
        }

        ClientWebSocket mWebSocket;

        readonly Uri mUri;

        readonly CancellationToken mCancelToken;
        readonly CancellationTokenSource mCancelTokenSource = new CancellationTokenSource();

        volatile bool mbIsTryingConnection = false;

        readonly string mName;
        readonly string mApiKey;
        readonly Action<string> mProcessMessage;

        readonly Action mOnClose;

        const int ReceiveChunkSize = 1024;
        const int SendChunkSize = 1024;

        const string BRANCH_ATTRIBUTE_CHANGED_TRIGGER_TYPE = "branchAttributeChanged";
        
        static readonly ILog mLog = LogManager.GetLogger(typeof(WebSocketClient));
    }
}