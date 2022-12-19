using System;
using System.Threading.Tasks;
using AngryWasp.Cli;

namespace Node.CliCommands
{
    public class Validators : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;
            
            var validators = await Blockchain.GetValidatorList().ConfigureAwait(false);
            CliHelper.Write($"Validators{Environment.NewLine}", ConsoleColor.Green);
            foreach (var v in validators)
                CliHelper.Write(v);
            
            return CliHelper.Complete();
        }
    }
}