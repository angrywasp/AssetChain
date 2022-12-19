using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Cli.Args;
using AngryWasp.Cli.DefaultCommands;
using AngryWasp.Cli.Prompts;
using AngryWasp.Cryptography;
using AngryWasp.Logger;
using Common;
using RpcClient.CliCommands;

namespace RpcClient
{
    class Program
    {
        private static AppSettings<AppSettingData> settings;

        public static AppSettings<AppSettingData> Settings => settings;

        public static async Task Main(string[] rawArgs)
        {
            Arguments args = Arguments.Parse(rawArgs);
            Log.CreateInstance();
            Log.Instance.AddWriter("buffer", new ApplicationLogWriter(new List<(ConsoleColor, string)>()));

            ApplicationLogWriter.HideInfo = true;
            Log.Instance.SupressConsoleOutput = true;
            var walletFileName = args.GetString("wallet-file", "AssetChain.RpcClient");

            Log.Instance.AddWriter("file", new FileLogWriter(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"{walletFileName}.log")));

            (settings, _) = AppSettings<AppSettingData>.Load(walletFileName);

            settings.AppData.RpcHost = args.GetString("rpc-host", settings.AppData.RpcHost);
            settings.AppData.RpcPort = args.GetUshort("rpc-port", settings.AppData.RpcPort).Value;

            bool needNewWallet = settings.WalletStore.Count == 0;

            string seed = null;
            string password = null;

            if (needNewWallet)
            {
                string a, b;
                Mnemonic mnemonic = new Mnemonic();

                PasswordPrompt.Get(out seed, "Enter a seed phrase to import a wallet");
                if (string.IsNullOrEmpty(seed) || mnemonic.CreateWalletFromSeed(seed, 0) == null)
                    seed = null;

                if (!PasswordPrompt.Get(out a, "Enter the password for your new wallet"))
                    WritePreLaunchError("Cancelled");

                if (!PasswordPrompt.Get(out b, "Confirm your password"))
                    WritePreLaunchError("Cancelled");

                if (a != b)
                    WritePreLaunchError("Passwords do not match");

                seed ??= mnemonic.CreateNewSeed();

                settings.CreateWallet("Default", a, seed);
                settings.Save();
                password = a;

                ApplicationLogWriter.WriteImmediate($"Wallet created");
            }
            else
            {
                if (!InputPrompt.Get("Enter your password", out password))
                    WritePreLaunchError("Cancelled at password prompt");

                if (!settings.WalletStore.CheckPassword(password))
                    WritePreLaunchError("Incorrect password");
            }

            await settings.SwitchWallets(settings.LastWallet, password).ConfigureAwait(false);
            ApplicationLogWriter.WriteImmediate($"Using wallet: {WalletStore.Current.Address}");

            Application.RegisterCommand("transfer", "Transfer some coins", new Transfer().Handle);
            Application.RegisterCommand("address", "Show your address", new Address().Handle);
            Application.RegisterCommand("balance", "Show your balance", new Balance().Handle);
            Application.RegisterCommand("clear", "Clear the console", new Clear().Handle);
            Application.RegisterCommand("help", "Print the help", new Help().Handle);

            Application.Start(loggerAlreadyAttached: true);
        }

        private static void WritePreLaunchError(string error)
        {
            ApplicationLogWriter.WriteImmediate($"Error: {error}{Environment.NewLine}", ConsoleColor.Red);
            Environment.Exit(1);
        }
    }
}
