using System.Threading.Tasks;
using AngryWasp.Logger;
using AngryWasp.Net;

namespace Node.NetworkMessages
{
    public class PeerInfoNetworkCommand
    {
        public const byte CODE = 12;

        public static byte[] GenerateRequest() => Header.Create(CODE).ToArray();

        public static async Task GenerateResponse(Connection c, Header h, byte[] d)
        {
            if (h.IsRequest)
            {
                var info = await PeerSyncInfo.Create().ConfigureAwait(false);
                var bin = PeerSyncInfo.ToBinary(info);
                var response = Header.Create(CODE, false, (ushort)bin.Count);
                response.AddRange(bin);
                
#pragma warning disable CS4014
                c.WriteAsync(response.ToArray());
#pragma warning restore CS4014
            }
            else
            {
                int offset = 0;
                PeerSyncInfo p;
                if (!PeerSyncInfo.FromBinary(d, ref offset, out p))
                {
                    Log.Instance.WriteWarning("Could not parse peer info");
                    return;
                }

                await SyncManager.PeerList.AddOrUpdate(c.PeerId, p).ConfigureAwait(false);
            }
        }
    }
}
