using System;
using System.Threading.Tasks;
using AngryWasp.Cli;
using Common;

namespace RpcClient.CliCommands
{
    public class Address : IApplicationCommand
    {
        public Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return Task.FromResult(false);
            
            CliHelper.Write("Address: ", ConsoleColor.Green);
            CliHelper.Write(WalletStore.Current.Address);
            
            return Task.FromResult(CliHelper.Complete());
        }
    }
}