using System.Threading.Tasks;
using AngryWasp.Logger;
using AngryWasp.Net;

namespace Node.NetworkMessages
{
    public class ShareTransactionNetworkCommand
    {
        public const byte CODE = 14;

        public static byte[] GenerateRequest(Transaction tx)
        {
            var requestData = tx.ToBinary();

            var request = Header.Create(CODE, true, (ushort)requestData.Count);
            request.AddRange(requestData);

            return request.ToArray();
        }

        public static async Task GenerateResponse(Connection c, Header h, byte[] d)
        {
            int offset = 0;
            Transaction tx;
            if (!Transaction.FromBinary(d, ref offset, out tx))
            {
                await ConnectionManager.RemoveAsync(c, "Failed to parse transaction").ConfigureAwait(false);
                return;
            }

            var txVerified = await tx.VerifyAsync().ConfigureAwait(false);

            if (!txVerified)
            {
                await ConnectionManager.RemoveAsync(c, "Invalid transaction").ConfigureAwait(false);
                return;
            }

            bool added = await Blockchain.AddToTxPoolAsync(tx).ConfigureAwait(false);

            if (!added)
                return;

            var msg = Header.Create(CODE, true, (ushort)d.Length);
            msg.AddRange(d);

#pragma warning disable CS4014
            MessageSender.BroadcastAsync(msg.ToArray(), c);
#pragma warning restore CS4014
        }
    }
}
