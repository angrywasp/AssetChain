using System;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Net;
using Common;

namespace Node.CliCommands
{
    public class Status : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;
            var head = await Blockchain.GetHeadAsync().ConfigureAwait(false);

            if (head == null)
                return CliHelper.Complete("Could not get chain head. Blockchain is not intiialized");

            bool isSynchronized = await Blockchain.GetIsSynchronizedAsync().ConfigureAwait(false);

            var con = await Blockchain.CheckConsensusAsync().ConfigureAwait(false);
            var bids = await Blockchain.GetSortedBidListAsync().ConfigureAwait(false);
            var connectionCount = await ConnectionManager.Count().ConfigureAwait(false);
            
            CliHelper.Write("     Address: ", ConsoleColor.Green);
            CliHelper.Write(WalletStore.Current.Address);

            CliHelper.Write("Synchronized: ", ConsoleColor.Green);
            CliHelper.Write(isSynchronized.ToString());

            CliHelper.Write("        Head: ", ConsoleColor.Green);
            CliHelper.Write(head.ToString());

            CliHelper.Write(" Connections: ", ConsoleColor.Green);
            CliHelper.Write(connectionCount.ToString());

            CliHelper.Write("  Validators: ", ConsoleColor.Green);

            if (bids != null)
            {
                CliHelper.Write($"{bids[0].Bid.Address}: ", ConsoleColor.Magenta);
                CliHelper.Write($"weight: {bids[0].Weight}");

                for (int i = 1; i < bids.Count; i++)
                {
                    CliHelper.Write($"              {bids[i].Bid.Address}: ", ConsoleColor.Magenta);
                    CliHelper.Write($"weight: {bids[i].Weight}");
                }
            }
            
            return CliHelper.Complete();
        }
    }
}