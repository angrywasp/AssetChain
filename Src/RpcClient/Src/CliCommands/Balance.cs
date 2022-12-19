using System;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Json.Rpc;
using Common;
using Newtonsoft.Json;

namespace RpcClient.CliCommands
{
    public class Balance : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;
            
            if (!EthAddress.TryParse(command, out EthAddress address))
                address = WalletStore.Current.Address;

            var r = await new JsonRpcClient($"http://{Program.Settings.AppData.RpcHost}", Program.Settings.AppData.RpcPort).SendRequest("balance", new JsonRequest<EthAddress>() {
                Data = address
            }.Serialize()).ConfigureAwait(false);

            var balance = JsonConvert.DeserializeObject<AccountBalance>(r);

            CliHelper.Write("  Current: ", ConsoleColor.Green);
            CliHelper.Write(balance.Current.FromAtomicUnits().ToCurrencyString(Constants.DECIMALS));
            CliHelper.Write("Available: ", ConsoleColor.Green);
            CliHelper.Write(balance.Available.FromAtomicUnits().ToCurrencyString(Constants.DECIMALS));
            
            return CliHelper.Complete();
        }
    }
}