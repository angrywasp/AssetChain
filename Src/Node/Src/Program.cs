using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Cli.Args;
using AngryWasp.Cli.DefaultCommands;
using AngryWasp.Cli.Prompts;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Json.Rpc;
using AngryWasp.Logger;
using AngryWasp.Net;
using Common;
using Node.CliCommands;
using Node.NetworkMessages;

namespace Node
{
    class Program
    {
        private static AppSettings<AppSettingData> settings;

        public static AppSettings<AppSettingData> Settings => settings;

        // Command line arguments
        // --wallet-file: path to a wallet file
        // --rpc: enable rpc server
        // --p2p-port: port to use for p2p comms
        // --rpc-port: port to use for rpc comms
        public static async Task Main(string[] rawArgs)
        {
            Arguments args = Arguments.Parse(rawArgs);
            Log.CreateInstance();
            Log.Instance.AddWriter("buffer", new ApplicationLogWriter(new List<(ConsoleColor, string)>()));

            ApplicationLogWriter.HideInfo = true;
            Log.Instance.SupressConsoleOutput = true;
            var walletFileName = args.GetString("wallet-file", "AssetChain.Node");

            var enableRpc = args["rpc"] != null;

            Log.Instance.AddWriter("file", new FileLogWriter(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), $"{walletFileName}.log")));

            (settings, _) = AppSettings<AppSettingData>.Load(walletFileName);

            settings.AppData.P2PPort = args.GetUshort("p2p-port", settings.AppData.P2PPort).Value;
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
                if (args["password"] == null)
                {
                    if (!InputPrompt.Get("Enter your password", out password))
                        WritePreLaunchError("Cancelled at password prompt");

                    if (!settings.WalletStore.CheckPassword(password))
                        WritePreLaunchError("Incorrect password");
                }
                else
                    password = args.GetString("password", string.Empty);
            }

            await settings.SwitchWallets(settings.LastWallet, password).ConfigureAwait(false);
            ApplicationLogWriter.WriteImmediate($"Using wallet: {WalletStore.Current.Address}");

            CommandProcessor.RegisterDefaultCommands();
            AngryWasp.Net.Config.SetNetId(Constants.CHAIN_ID);

            //TimedEventManager.RegisterEvent("PingPeers", new PingPeersTimedEvent().Execute, 30);
            TimedEventManager.RegisterEvent("ExchangePeerLists", new ExchangePeerListTimedEvent().Execute, 120);
            TimedEventManager.RegisterEvent("SyncTransactionPool", new SyncTransactionPoolTimedEvent().Execute, 90);
            TimedEventManager.RegisterEvent("SyncBlockchain", new SyncBlockchainTimedEvent().Execute, 30);
            TimedEventManager.RegisterEvent("VotePool", new BlockVoteTimedEvent().Execute, 15);
            TimedEventManager.RegisterEvent("Consensus", new ConsensusTimedEvent().Execute, 5);
            TimedEventManager.RegisterEvent("CleanPools", new CleanPoolsTimedEvent().Execute, 60);
            TimedEventManager.RegisterEvent("PeerInfo", new PeerInfoTimedEvent().Execute, 45);
            TimedEventManager.RegisterEvent("ConnectToPeerList", new ConnectToPeerListTimedEvent().Execute, 10);

            Application.RegisterCommand("fetch_peers", "Ask your peers for more connections", new FetchPeers().Handle);
            Application.RegisterCommand("peers", "Print a list of connected peers", new PrintPeers().Handle);
            Application.RegisterCommand("transfer", "Transfer some coins", new Transfer().Handle);
            Application.RegisterCommand("address", "Show your address", new Address().Handle);
            Application.RegisterCommand("balance", "Show your balance", new Balance().Handle);
            Application.RegisterCommand("print_pool", "Print the TX pool", new PrintPools().Handle);
            Application.RegisterCommand("print_chain", "Print the Blockchain", new PrintChain().Handle);
            Application.RegisterCommand("status", "Print the node status", new Status().Handle);
            Application.RegisterCommand("validators", "Show a list of validators", new Validators().Handle);
            Application.RegisterCommand("add_validator", "Register as a validator", new AddValidator().Handle);
            Application.RegisterCommand("clear", "Clear the console", new Clear().Handle);
            Application.RegisterCommand("help", "Print the help", new Help().Handle);

            CommandProcessor.RegisterCommand("ShareBlock", ShareBlockNetworkCommand.CODE, ShareBlockNetworkCommand.GenerateResponse);
            CommandProcessor.RegisterCommand("ShareTransaction", ShareTransactionNetworkCommand.CODE, ShareTransactionNetworkCommand.GenerateResponse);
            CommandProcessor.RegisterCommand("SyncTransactionPool", SyncTransactionPoolNetworkCommand.CODE, SyncTransactionPoolNetworkCommand.GenerateResponse);
            CommandProcessor.RegisterCommand("SyncVotingPool", SyncVotingPoolNetworkCommand.CODE, SyncVotingPoolNetworkCommand.GenerateResponse);
            CommandProcessor.RegisterCommand("SyncBlockchain", SyncBlockchainNetworkCommand.CODE, SyncBlockchainNetworkCommand.GenerateResponse);
            CommandProcessor.RegisterCommand("SyncBlock", SyncBlockNetworkCommand.CODE, SyncBlockNetworkCommand.GenerateResponse);
            CommandProcessor.RegisterCommand("Bid", BidNetworkCommand.CODE, BidNetworkCommand.GenerateResponse);
            CommandProcessor.RegisterCommand("Vote", VoteNetworkCommand.CODE, VoteNetworkCommand.GenerateResponse);
            CommandProcessor.RegisterCommand("PeerInfo", PeerInfoNetworkCommand.CODE, PeerInfoNetworkCommand.GenerateResponse);

            Database.Initialize(walletFileName);
            await Blockchain.Load().ConfigureAwait(false);

            ConnectionManager.Added += (Connection c) =>
            {
                Task.Run(async () =>
                {
                    await Database.InsertPeer(c, DateTimeHelper.TimestampNow).ConfigureAwait(false);

                    { // Get additional peer info
                        await Task.Delay(1000).ConfigureAwait(false);
                        var request = PeerInfoNetworkCommand.GenerateRequest();
                        await c.WriteAsync(request).ConfigureAwait(false);
                    }

                    { // Blockchain sync
                        await Task.Delay(1000).ConfigureAwait(false);
                        var request = await SyncBlockchainNetworkCommand.GenerateRequest().ConfigureAwait(false);
                        await MessageSender.BroadcastAsync(request).ConfigureAwait(false);
                    }

                    { // TX Pool sync
                        //await Task.Delay(1000).ConfigureAwait(false);
                        var request = await SyncTransactionPoolNetworkCommand.GenerateRequest().ConfigureAwait(false);
                        await MessageSender.BroadcastAsync(request).ConfigureAwait(false);
                    }

                    { // Voting pool sync
                        await Task.Delay(1000).ConfigureAwait(false);
                        var request = await SyncVotingPoolNetworkCommand.GenerateRequest().ConfigureAwait(false);
                        await MessageSender.BroadcastAsync(request).ConfigureAwait(false);
                    }

                    { // Exchange peer list
                        await Task.Delay(1000).ConfigureAwait(false);
                        var request = await ExchangePeerList.GenerateRequest(true, null).ConfigureAwait(false);
                        await c.WriteAsync(request).ConfigureAwait(false);
                    }
                });
            };

            foreach (var a in args.All)
            {
                if (a.Flag != "add-peer")
                    continue;

                if (string.IsNullOrEmpty(a.Value))
                    continue;

                var split = a.Value.Split(":", StringSplitOptions.RemoveEmptyEntries);
                var port = split.Length > 1 ? ushort.Parse(split[1]) : Constants.DEFAULT_P2P_PORT;
                AngryWasp.Net.Client.ConnectHost(split[0], port);
            }

            new AngryWasp.Net.Server().Start(settings.AppData.P2PPort, WalletStore.Current.Address.ToByte());

            await Helpers.ConnectToPeerList().ConfigureAwait(false);

            if (enableRpc)
            {
                JsonRpcServer server = new JsonRpcServer(settings.AppData.RpcPort);
                server.RegisterCommands();
                server.Start();
            }

            Application.Start(loggerAlreadyAttached: true);
        }

        private static void WritePreLaunchError(string error)
        {
            ApplicationLogWriter.WriteImmediate($"Error: {error}{Environment.NewLine}", ConsoleColor.Red);
            Environment.Exit(1);
        }
    }
}
