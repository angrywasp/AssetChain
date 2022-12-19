using System.Threading.Tasks;
using AngryWasp.Net;

namespace Node.NetworkMessages
{
    public class VoteNetworkCommand
    {
        public const byte CODE = 19;

        public static byte[] GenerateRequest(NodeVote vote)
        {
            var requestData = vote.ToBinary();
            var request = Header.Create(CODE, true, (ushort)requestData.Count);
            request.AddRange(requestData);
            return request.ToArray();
        }

        public static async Task GenerateResponse(Connection c, Header h, byte[] d)
        {
            var isSynchronized = await Blockchain.GetIsSynchronizedAsync().ConfigureAwait(false);
            if (!isSynchronized)
                return;

            int offset = 0;
            var vote = NodeVote.FromBinary(d, ref offset);

            var verified = await vote.VerifyAsync().ConfigureAwait(false);
            if (!verified)
            {
                await ConnectionManager.RemoveAsync(c, "Invalid vote").ConfigureAwait(false);
                return;
            }

            bool added = await Blockchain.AddToVotePoolAsync(vote).ConfigureAwait(false);

            if (!added)
                return;

            var msg = Header.Create(CODE, true, (ushort)d.Length);
            msg.AddRange(d);
            await MessageSender.BroadcastAsync(msg.ToArray(), c).ConfigureAwait(false);
        }
    }
}
