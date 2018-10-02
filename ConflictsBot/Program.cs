using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Xml;
using log4net;
using log4net.Config;

namespace ConflictsBot
{
    class Program
    {
        static void Main(string[] args)
        {
            ConfigureLogging();

                if (args != null)
                    mLog.DebugFormat("Args: {0}", string.Join(" ", args));

            Console.WriteLine("Hello World!");
        }

        static void ConfigureLogging()
        {
            try
            {
                string log4netpath = ToolConfig.GetHalBotLogConfigFile();

                XmlDocument log4netConfig = new XmlDocument();
                log4netConfig.Load(File.OpenRead(log4netpath));

                var repo = log4net.LogManager.CreateRepository(
                    Assembly.GetEntryAssembly(),
                    typeof(log4net.Repository.Hierarchy.Hierarchy));

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

        static readonly ILog mLog = LogManager.GetLogger(typeof(Program));

    }
}
