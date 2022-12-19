using System.Threading.Tasks;
using AngryWasp.Net;

namespace Node.NetworkMessages
{
    public class BidNetworkCommand
    {
        public const byte CODE = 11;

        public static byte[] GenerateRequest(NodeBid bid)
        {
            var requestData = bid.ToBinary();

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
            var bid = NodeBid.FromBinary(d, ref offset);

            bool verified = await bid.VerifyAsync().ConfigureAwait(false);

            if (!verified)
            {
                await ConnectionManager.RemoveAsync(c, "Invalid bid").ConfigureAwait(false);
                return;
            }

            bool added = await Blockchain.AddToBidPoolAsync(bid).ConfigureAwait(false);

            if (!added)
                return;

            var msg = Header.Create(CODE, true, (ushort)d.Length);
            msg.AddRange(d);

            await MessageSender.BroadcastAsync(msg.ToArray(), c).ConfigureAwait(false);
        }
    }
}
