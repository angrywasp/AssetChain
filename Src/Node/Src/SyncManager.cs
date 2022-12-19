using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Net;

namespace Node
{
    public class PeerSyncInfo
    {
        public int TopBlockIndex { get; private set; }
        public HashKey32 TopBlockHash { get; private set; }

        public static async Task<PeerSyncInfo> Create()
        {
            var head = await Blockchain.GetHeadAsync().ConfigureAwait(false);

            var p = new PeerSyncInfo();
            p.TopBlockIndex = head.Index;
            p.TopBlockHash = head.Block.Hash;

            return p;
        }

        public static List<byte> ToBinary(PeerSyncInfo p)
        {
            var bin = new List<byte>();
            bin.AddRange(p.TopBlockIndex.ToByte());
            bin.AddRange(p.TopBlockHash);
            return bin;
        }

        public static bool FromBinary(byte[] bin, ref int offset, out PeerSyncInfo p)
        {
            if (bin.Length < 36)
            {
                p = null;
                return false;
            }

            p = new PeerSyncInfo();
            p.TopBlockIndex = bin.ToInt(ref offset);
            p.TopBlockHash = bin.Skip(offset).Take(32).ToArray();
            offset += 32;
            return true;
        }
    }

    public static class SyncManager
    {
        private static ThreadSafeDictionary<ConnectionId, PeerSyncInfo> peerList =
            new ThreadSafeDictionary<ConnectionId, PeerSyncInfo>();

        public static ThreadSafeDictionary<ConnectionId, PeerSyncInfo> PeerList => peerList;
    }
}