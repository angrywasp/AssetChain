using System.Threading.Tasks;
using AngryWasp.Net;
using Common;
using Node.NetworkMessages;

namespace Node
{
    //Look for more peers in peer list if we fall below the minimum number of required peers
    public class ConnectToPeerListTimedEvent : ITimedEvent
    {
        public async Task Execute()
        {
            var count = await ConnectionManager.Count().ConfigureAwait(false);
            if (count < Constants.MINIMUM_PEERS)
                await Helpers.ConnectToPeerList().ConfigureAwait(false);
        }
    }

    //Swap peer lists with your connected peers
    public class ExchangePeerListTimedEvent : ITimedEvent
    {
        public async Task Execute()
        {
            var request = await ExchangePeerList.GenerateRequest(true, null).ConfigureAwait(false);
            await MessageSender.BroadcastAsync(request).ConfigureAwait(false);
        }
    }

    public class SyncTransactionPoolTimedEvent : ITimedEvent
    {
        public async Task Execute()
        {
            var request = await SyncTransactionPoolNetworkCommand.GenerateRequest().ConfigureAwait(false);
            await MessageSender.BroadcastAsync(request).ConfigureAwait(false);
        }
    }

    public class SyncBlockchainTimedEvent : ITimedEvent
    {
        public async Task Execute()
        {
            while (true)
            {
                var request = await SyncBlockchainNetworkCommand.GenerateRequest().ConfigureAwait(false);

                var c = await ConnectionManager.GetRandomConnection().ConfigureAwait(false);
                if (c == null)
                    break;

                var writeOk = await c.WriteAsync(request).ConfigureAwait(false);
                if (writeOk)
                    break;
            }
        }
    }

    public class CleanPoolsTimedEvent : ITimedEvent
    {
        public async Task Execute()
        {
            await Blockchain.CleanPoolsAsync().ConfigureAwait(false);
        }
    }

    public class BlockVoteTimedEvent : ITimedEvent
    {
        public async Task Execute()
        {
            bool isSynchronized = await Blockchain.GetIsSynchronizedAsync().ConfigureAwait(false);
            if (!isSynchronized)
                return;

            var request = await SyncVotingPoolNetworkCommand.GenerateRequest().ConfigureAwait(false);
            await MessageSender.BroadcastAsync(request).ConfigureAwait(false);
        }
    }

    public class PeerInfoTimedEvent : ITimedEvent
    {
        public async Task Execute()
        {
            await MessageSender.BroadcastAsync(PeerInfoNetworkCommand.GenerateRequest()).ConfigureAwait(false);
        }
    }

    public class ConsensusTimedEvent : ITimedEvent
    {
        public async Task Execute()
        {
            bool isSynchronized = await Blockchain.GetIsSynchronizedAsync().ConfigureAwait(false);
            if (!isSynchronized)
                return;

            await Blockchain.CheckConsensusAsync().ConfigureAwait(false);
        }
    }
}