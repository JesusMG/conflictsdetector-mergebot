using System;

namespace ConflictsBot
{
    internal class BotArguments
    {
        internal BotArguments(string[] args)
        {
            mArgs = args;
        }

        internal void Parse()
        {
            LoadArguments(mArgs);
        }

        internal bool AreValidArgs
        {
            get { return mbAreValidArgs; }
        }

        internal bool HasToShowUsage
        {
            get { return mShowUsage; }
        }

        internal string WebSocketUrl
        {
            get { return mWebSocketUrl; }
        }

        internal string RestApiUrl
        {
            get { return mRestApiUrl; }
        }

        internal string BotName
        {
            get { return mBotName; }
        }

        internal string ApiKey
        {
            get { return mApiKey; }
        }

        internal string ConfigFilePath
        {
            get { return mConfigFilePath; }
        }

        void LoadArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                mShowUsage = true;
                return;
            }

            mbAreValidArgs = true;
            for (int i = 0; i < args.Length; i++)
            {
                if (!mbAreValidArgs)
                    return;

                if (args[i] == null)
                {
                    continue;
                }

                if (IsUsageArgument(args[i]))
                {
                    mShowUsage = true;
                    return;
                }

                if (args[i] == WEB_SOCKET_URL_ARG)
                {
                    mbAreValidArgs = ReadArgumentValue(args, ++i, out mWebSocketUrl);
                    continue;
                }

                if (args[i] == API_URL_ARG)
                {
                    mbAreValidArgs = ReadArgumentValue(args, ++i, out mRestApiUrl);
                    continue;
                }

                if (args[i] == BOT_NAME_ARG)
                {
                    mbAreValidArgs = ReadArgumentValue(args, ++i, out mBotName);
                    continue;
                }

                if (args[i] == API_KEY_ARG)
                {
                    mbAreValidArgs = ReadArgumentValue(args, ++i, out mApiKey);
                    continue;
                }

                if (args[i] == CONFIG_FILE_ARG)
                {
                    mbAreValidArgs = ReadArgumentValue(args, ++i, out mConfigFilePath);
                    continue;
                }
            }
        }

        static bool ReadArgumentValue(string[] args, int argIndex, out string value)
        {
            value = string.Empty;
            if (argIndex >= args.Length)
                return false;

            value = args[argIndex].Trim();

            foreach (string validArgName in VALID_ARGS_NAMES)
                if (value.Equals(validArgName))
                    return false;

            return !value.Equals(string.Empty);
        }

        static bool IsUsageArgument(string argument)
        {
            foreach (string validHelpArg in VALID_HELP_ARGS)
                if (argument == validHelpArg)
                    return true;

            return false;
        }

        readonly string[] mArgs;

        bool mShowUsage = false;
        string mWebSocketUrl;
        string mRestApiUrl;
        string mBotName;
        string mApiKey;
        string mConfigFilePath;

        bool mbAreValidArgs;

        static readonly string[] VALID_HELP_ARGS = new string[] {
            "--help", "-h", "--?", "-?" };

        static readonly string[] VALID_ARGS_NAMES = new string[] {
            WEB_SOCKET_URL_ARG,
            API_URL_ARG,
            BOT_NAME_ARG,
            API_KEY_ARG,
            CONFIG_FILE_ARG };

        const string WEB_SOCKET_URL_ARG = "--websocket";
        const string API_URL_ARG = "--restapi";
        const string BOT_NAME_ARG = "--name";
        const string API_KEY_ARG = "--apikey";
        const string CONFIG_FILE_ARG = "--config";
    }
}