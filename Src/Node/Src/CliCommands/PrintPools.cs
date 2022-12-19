using System;
using System.Threading.Tasks;
using AngryWasp.Cli;

namespace Node.CliCommands
{
    public class PrintPools : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;

            CliHelper.Write($"Block Pool:{Environment.NewLine}", ConsoleColor.Green);
            var blockPool = await Blockchain.GetBlockPoolAsync().ConfigureAwait(false);
            foreach (var blk in blockPool.Values)
                CliHelper.Write(blk.ToString());

            CliHelper.Write($"TX Pool:{Environment.NewLine}", ConsoleColor.Green);
            var txPool = await Blockchain.GetTransactionPoolAsync().ConfigureAwait(false);
            foreach (var tx in txPool.Values)
                CliHelper.Write(tx.ToString());

            CliHelper.Write($"Bid Pool:{Environment.NewLine}", ConsoleColor.Green);
            var bidPool = await Blockchain.GetBidPoolAsync().ConfigureAwait(false);
            foreach (var bid in bidPool.Values)
                CliHelper.Write(bid.ToString());

            CliHelper.Write($"Vote Pool:{Environment.NewLine}", ConsoleColor.Green);
            var votePool = await Blockchain.GetVotePoolAsync().ConfigureAwait(false);
            foreach (var vote in votePool.Values)
            {
                foreach (var v in vote.Values)
                   CliHelper.Write(v.ToString());
            }
            
            return CliHelper.Complete();
        }
    }
}