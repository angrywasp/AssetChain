using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AngryWasp.Helpers;
using AngryWasp.Logger;
using AngryWasp.Net;

namespace Node.NetworkMessages
{
    public class SyncBlockNetworkCommand
    {
        public const byte CODE = 15;

        public static byte[] GenerateRequest(int index, int count)
        {
            var requestData = new List<byte>();
            requestData.AddRange(index.ToByte());
            requestData.AddRange(count.ToByte());

            var request = Header.Create(CODE, true, (ushort)requestData.Count);
            request.AddRange(requestData);

            return request.ToArray();
        }

        public static async Task GenerateResponse(Connection c, Header h, byte[] d)
        {
            if (h.IsRequest)
            {
                int offset = 0;
                int index = d.ToInt(ref offset);
                int requested = d.ToInt(ref offset);

                var head = await Blockchain.GetHeadAsync().ConfigureAwait(false);

                if (head == null)
                    Debugger.Break();

                if (head.Index < index)
                    return;

                var blockData = new List<byte>();

                var blocks = await Blockchain.GetBlocksAsync(index, Math.Min(requested, 10)).ConfigureAwait(false);

                int count = 0;
                foreach (var blk in blocks)
                {
                    blockData.AddRange(blk.ToBinary());
                    count++;
                }

                Log.Instance.WriteInfo($"{c.PeerId} requested {count} blocks, starting at index {index}");

                var request = Header.Create(CODE, false, (ushort)(blockData.Count + 8));
                request.AddRange(index.ToByte());
                request.AddRange(count.ToByte());
                request.AddRange(blockData);

#pragma warning disable CS4014
                c.WriteAsync(request.ToArray());
#pragma warning restore CS4014
            }
            else
            {
                int offset = 0;
                int index = d.ToInt(ref offset);
                int count = d.ToInt(ref offset);

                List<Block> blocks = new List<Block>();

                for (var i = 0; i < count; i++)
                {
                    Block blk;
                    if (!Block.FromBinary(d, ref offset, out blk))
                    {
                        await ConnectionManager.RemoveAsync(c, "Failed to parse block").ConfigureAwait(false);
                        return;
                    }

                    blocks.Add(blk);
                }

                await Blockchain.HandleIncomingBlocksAsync(index, blocks).ConfigureAwait(false);
            }
        }
    }
}
