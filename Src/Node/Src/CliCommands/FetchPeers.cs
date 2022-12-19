using System.Linq;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Net;

namespace Node.CliCommands
{
    public class FetchPeers : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;

            CliHelper.Write("Fetching additional peers");
            var request = await ExchangePeerList.GenerateRequest(true, null).ConfigureAwait(false);
            await MessageSender.BroadcastAsync(request.ToArray()).ConfigureAwait(false);
            
            return CliHelper.Complete();
        }
    }
}