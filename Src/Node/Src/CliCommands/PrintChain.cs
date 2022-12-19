using System;
using System.Threading.Tasks;
using AngryWasp.Cli;

namespace Node.CliCommands
{
    public class PrintChain : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;
            
            bool isSynchronized = await Blockchain.GetIsSynchronizedAsync().ConfigureAwait(false);
            
            var head = await Blockchain.GetHeadAsync().ConfigureAwait(false);
            if (head == null)
                return CliHelper.Complete("Could not get chain head. Blockchain is not intiialized");

            CliHelper.Write($"Synchronized: {isSynchronized.ToString()}");
            for (var i = 0; i <= head.Index; i++)
            {
                var blk = await Blockchain.GetBlockAsync(i).ConfigureAwait(false);
                CliHelper.Write($"Block {i}{Environment.NewLine}", ConsoleColor.Green);
                CliHelper.Write(blk.ToString());
            }
            
            return CliHelper.Complete();
        }
    }
}