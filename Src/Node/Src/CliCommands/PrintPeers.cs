using AngryWasp.Cli;
using AngryWasp.Net;
using System;
using System.Threading.Tasks;

namespace Node.CliCommands
{
    public class PrintPeers : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;

            await ConnectionManager.ForEach(Direction.Incoming | Direction.Outgoing, async (c) =>
            {
                PeerSyncInfo p = await SyncManager.PeerList.Get(c.PeerId).ConfigureAwait(false);

                Console.WriteLine($"{c.PeerId} - {c.Address.MapToIPv4()}:{c.Port}");
                if (p == null)
                    Console.WriteLine($"{string.Empty.PadRight(45)}No sync info");
                else 
                    Console.WriteLine($"{string.Empty.PadRight(45)}{p.TopBlockIndex}:{p.TopBlockHash}");
            }).ConfigureAwait(false);

            return CliHelper.Complete();
        }
    }
}