using System;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using Common;

namespace Node.CliCommands
{
    public class Balance : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;
            
            if (!EthAddress.TryParse(command, out EthAddress address))
                address = WalletStore.Current.Address;
            
            var balance = await Blockchain.GetBalanceOfAsync(address).ConfigureAwait(false);
            CliHelper.Write("  Current: ", ConsoleColor.Green);
            CliHelper.Write(balance.Current.FromAtomicUnits().ToCurrencyString(Constants.DECIMALS));
            CliHelper.Write("Available: ", ConsoleColor.Green);
            CliHelper.Write(balance.Available.FromAtomicUnits().ToCurrencyString(Constants.DECIMALS));
            
            return CliHelper.Complete();
        }
    }
}