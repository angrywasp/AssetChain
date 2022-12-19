using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Logger;
using AngryWasp.Net;

namespace Node.NetworkMessages
{
    public class SyncBlockchainNetworkCommand
    {
        public const byte CODE = 16;

        public static async Task<byte[]> GenerateRequest()
        {
            var expectedResponses = await ConnectionManager.Count().ConfigureAwait(false);
            await Blockchain.InitiateSyncAsync(expectedResponses).ConfigureAwait(false);
            return Header.Create(CODE).ToArray();
        }

        public static async Task GenerateResponse(Connection c, Header h, byte[] d)
        {
            if (h.IsRequest)
            {
                var head = await Blockchain.GetHeadAsync().ConfigureAwait(false);

                if (head == null)
                    return;

                var responseData = new List<byte>();
                responseData.AddRange(head.Index.ToByte());
                responseData.AddRange(head.Block.Hash);

                var response = Header.Create(CODE, false, (ushort)responseData.Count);
                response.AddRange(responseData);

#pragma warning disable CS4014
                c.WriteAsync(response.ToArray());
#pragma warning restore CS4014
            }
            else
            {
                var offset = 0;
                var responseIndex = d.ToInt(ref offset);
                HashKey32 responseHash = d.Skip(offset).Take(32).ToArray();

                int syncThreshold = 0;
                int blockSize = 10;

                var response = await Blockchain.ProcessSyncResponseAsync(responseIndex, responseHash, c, syncThreshold).ConfigureAwait(false);

                if (response.NewHeight == -1 || response.RequiredBlocks == -1)
                    return;

                int topIndex = response.NewHeight;
                var startIndex = (topIndex - response.RequiredBlocks) + 1;
                var count = topIndex - startIndex + 1;

                Log.Instance.WriteInfo($"{count} blocks required");
                if (count <= syncThreshold)
                    return;

                int blockGroups = count / blockSize;

                for (int i = 0; i < blockGroups; i++)
                {
                    await RequestBlockGroup(startIndex, blockSize).ConfigureAwait(false);
                    startIndex += blockSize;
                    count -= blockSize;
                }

                await RequestBlockGroup(startIndex, count).ConfigureAwait(false);
            }
        }

        private static async Task<bool> RequestBlockGroup(int startIndex, int count)
        {
            while (true)
            {
                var blockRequest = SyncBlockNetworkCommand.GenerateRequest(startIndex, count).ToArray();

                var rConn = await ConnectionManager.GetRandomConnection().ConfigureAwait(false);
                if (rConn == null)
                    return false;

                var writeOk = await rConn.WriteAsync(blockRequest).ConfigureAwait(false);
                if (writeOk)
                {
                    Log.Instance.WriteInfo($"Requesting {count} blocks starting at index {startIndex} from {rConn.PeerId.ToString()}");
                    break;
                }
            }

            return true;
        }
    }
}