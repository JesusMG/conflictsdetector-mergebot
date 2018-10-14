using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using log4net;
using log4net.Config;

namespace ConflictsBot
{
    class Program
    {
        static int Main(string[] args)
        {            
            BotArguments botArgs = new BotArguments(args);
            botArgs.Parse();

            ConfigureLogging(botArgs.BotName);

            string argsStr = args == null ? string.Empty : string.Join(" ", args);
            mLog.DebugFormat("Args: [{0}]. Are valid args?: [{1}]", argsStr, botArgs.AreValidArgs);

            if (!botArgs.AreValidArgs || botArgs.HasToShowUsage)
            {
                PrintUsage(botArgs.AreValidArgs);
                return 0;
            }

            BotConfiguration botConfig = BotConfiguration.Build(
                botArgs.ConfigFilePath,
                botArgs.RestApiUrl,
                botArgs.WebSocketUrl);

            string errorMessage = null;
            if (!BotConfigurationChecker.CheckConfiguration(botConfig, out errorMessage))
            {
                Console.WriteLine(errorMessage);
                mLog.ErrorFormat(
                    "Bot [{0}] is going to finish: error found on argument check.", botArgs.BotName);

                mLog.Error(errorMessage);
                return 1;
            }

            ConfigureServicePoint();

            LaunchBot(
                botArgs.WebSocketUrl,
                botArgs.RestApiUrl,
                botConfig,
                ToolConfig.GetResolvedBranchesStorageFile(GetEscapedBotName(botArgs.BotName)),
                ToolConfig.GetReadyToMergeBranchesStorageFile(GetEscapedBotName(botArgs.BotName)),
                botArgs.BotName,
                botArgs.ApiKey);

            mLog.InfoFormat(
                "Bot [{0}] is going to finish: orderly shutdown.", botArgs.BotName);

            return 0;
        }

        static void LaunchBot(
            string webSocketUrl, 
            string restApiUrl, 
            BotConfiguration botConfig, 
            string resolvedBranchesQueueFile,
            string readyToMergeBranchesFile,
            string botName, 
            string apiKey)
        {
            if (!Directory.Exists(Path.GetDirectoryName(resolvedBranchesQueueFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(resolvedBranchesQueueFile));

            if (!Directory.Exists(Path.GetDirectoryName(readyToMergeBranchesFile)))
                Directory.CreateDirectory(Path.GetDirectoryName(readyToMergeBranchesFile));

            FileStorage resolvedBranchesQueueStorage = new FileStorage(resolvedBranchesQueueFile);
            FileStorage readyToMergeBranchesStorage = new FileStorage(readyToMergeBranchesFile);

            IRestApi restApi = new RestApi(restApiUrl, botConfig.PlasticBotUserToken);

            ConflictsCheckerBot bot = new ConflictsCheckerBot(
                restApi,  
                botConfig, 
                resolvedBranchesQueueStorage,
                readyToMergeBranchesStorage,
                botName);

            try
            {
                bot.LoadBranchesToProcess();
            }
            catch (Exception e)
            {
                mLog.FatalFormat(
                    "ConflictsBot [{0}] is going to finish because it couldn't load " +
                    "the branches to process on startup. Reason: {1}", botName, e.Message);
                mLog.DebugFormat("Stack trace:{0}{1}", Environment.NewLine, e.StackTrace);
                throw;
            }

            ThreadPool.QueueUserWorkItem(bot.ProcessBranches);

            WebSocketClient ws = new WebSocketClient(
                webSocketUrl,
                botName,
                apiKey,
                bot.OnEventReceived);

            ws.ConnectWithRetries();

            Task.Delay(-1).Wait();
        }

        static void PrintUsage(bool bAreValidArgs)
        {
            Console.WriteLine(string.Format("{0}Usage:", bAreValidArgs ? string.Empty : "Invalid arguments. "));
            Console.WriteLine("\tConflictsBot.exe --server <WEB_SOCKET_URL> --config <JSON_CONFIG_FILE_PATH>");
            Console.WriteLine("\t                --apikey <WEB_SOCKET_CONN_KEY> --name <PLUG_NAME>");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("\tConflictsBot.exe --server wss://localhost:7111/plug --name jenkis-main ");
            Console.WriteLine("\t                --apikey 014B6147A6391E9F4F9AE67501ED690DC2D814FECBA0C1687D016575D4673EE3");
            Console.WriteLine("\t                --config conflictsbot.conf");
            Console.WriteLine();
        }

        static void ConfigureLogging(string botName)
        {
            if (string.IsNullOrEmpty(botName))
                botName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm");

            try
            {
                string log4netpath = ToolConfig.GetLogConfigFile();

                XmlDocument log4netConfig = new XmlDocument();
                log4netConfig.Load(File.OpenRead(log4netpath));

                var repo = log4net.LogManager.CreateRepository(
                    Assembly.GetEntryAssembly(),
                    typeof(log4net.Repository.Hierarchy.Hierarchy));

                log4net.GlobalContext.Properties["Name"] = botName;
         

                log4net.Config.XmlConfigurator.Configure(repo, log4netConfig["log4net"]);
            }
            catch
            {
                //it failed configuring the logging info; nothing to do.
            }
        }

        static void ConfigureServicePoint()
        {
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 500;
        }

        static string GetEscapedBotName(string botName)
        {
            char[] forbiddenChars = new char[] {
                '/', '\\', '<', '>', ':', '"', '|', '?', '*', ' ' };

            string cleanName = botName;
            if (botName.IndexOfAny(forbiddenChars) != -1)
            {
                foreach (char character in forbiddenChars)
                    cleanName = cleanName.Replace(character, '-');
            }

            return cleanName;
        }

        static readonly ILog mLog = LogManager.GetLogger(typeof(Program));
    }
}
