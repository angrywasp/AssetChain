using System.Collections.Generic;
using System.Threading.Tasks;
using AngryWasp.Helpers;
using AngryWasp.Logger;
using AngryWasp.Net;

namespace Node.NetworkMessages
{
    public class ShareBlockNetworkCommand
    {
        public const byte CODE = 13;

        public static byte[] GenerateRequest(int index, Block blk)
        {
            var requestData = new List<byte>();
            requestData.AddRange(index.ToByte());
            requestData.AddRange(blk.ToBinary());

            var request = Header.Create(CODE, true, (ushort)requestData.Count);
            request.AddRange(requestData);
            
            return request.ToArray();
        }

        public static async Task GenerateResponse(Connection c, Header h, byte[] d)
        {
            int offset = 0;
            int index = d.ToInt(ref offset);

            Block blk;
            if (!Block.FromBinary(d, ref offset, out blk))
            {
                await ConnectionManager.RemoveAsync(c, "Failed to parse block").ConfigureAwait(false);
                return;
            }

            var processed = await Blockchain.HandleIncomingBlockAsync(index, blk).ConfigureAwait(false);

            if (!processed.HasValue)
                return;

            if (!processed.Value)
            {
                await ConnectionManager.RemoveAsync(c, "Shared invalid block").ConfigureAwait(false);
                return;
            }

            var msg = Header.Create(CODE, true, (ushort)d.Length);
            msg.AddRange(d);

#pragma warning disable CS4014
            MessageSender.BroadcastAsync(msg.ToArray(), c);
#pragma warning restore CS4014
        }
    }
}
