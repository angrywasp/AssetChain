using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Net;

namespace Node.NetworkMessages
{
    public class SyncVotingPoolNetworkCommand
    {
        public const byte CODE = 18;

        public static async Task<byte[]> GenerateRequest()
        {
            List<byte> requestData = new List<byte>();

            var bidPool = await Blockchain.GetBidPoolAsync().ConfigureAwait(false);
            requestData.AddRange(bidPool.Count.ToByte());
            foreach (var bid in bidPool.Keys)
                requestData.AddRange(bid);

            var votePool = await Blockchain.GetVotePoolAsync().ConfigureAwait(false);
            requestData.AddRange(votePool.Count.ToByte());

            foreach (var v in votePool)
            {
                requestData.AddRange(v.Key);
                requestData.AddRange(v.Value.Count.ToByte());
                foreach (var vv in v.Value.Keys)
                    requestData.AddRange(vv);
            }

            var request = Header.Create(CODE, true, (ushort)requestData.Count);
            request.AddRange(requestData);
            return request.ToArray();
        }

        public static async Task GenerateResponse(Connection c, Header h, byte[] d)
        {
            if (h.IsRequest)
            {
                int offset = 0;
                int bidPoolCount = d.ToInt(ref offset);

                var hashSet = new HashSet<HashKey32>();
                for (int i = 0; i < bidPoolCount; i++)
                {
                    hashSet.Add(d.Skip(offset).Take(32).ToArray());
                    offset += 32;
                }

                var bidPool = await Blockchain.GetBidPoolAsync().ConfigureAwait(false);
                var bids = bidPool.Where(x => !hashSet.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);

                List<byte> responseData = new List<byte>();
                responseData.AddRange(bids.Count.ToByte());

                foreach (var bid in bids.Values)
                    responseData.AddRange(bid.ToBinary());

                int votePoolCount = d.ToInt(ref offset);
                var votePool = await Blockchain.GetVotePoolAsync().ConfigureAwait(false);

                var responseVotePool = new Dictionary<HashKey32, Dictionary<HashKey32, NodeVote>>();

                for (int i = 0; i < votePoolCount; i++)
                {
                    HashKey32 key = d.Skip(offset).Take(32).ToArray();
                    offset += 32;
                    int count = d.ToInt(ref offset);
                    var voteHashSet = new HashSet<HashKey32>();
                    for (int j = 0; j < count; j++)
                    {
                        voteHashSet.Add(d.Skip(offset).Take(32).ToArray());
                        offset += 32;
                    }

                    if (!votePool.ContainsKey(key))
                        continue;

                    var votes = votePool[key].Where(x => !voteHashSet.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
                    responseVotePool.Add(key, votes);
                }

                //the above code fills in missing votes for the bid hashes supplied by the client
                //now we loop through all the vote pools and fill in any we didn't receive from the client
                foreach (var v in votePool)
                {
                    if (responseVotePool.ContainsKey(v.Key))
                        continue;

                    responseVotePool.Add(v.Key, v.Value);
                }

                responseData.AddRange(responseVotePool.Count.ToByte());

                foreach (var v in responseVotePool)
                {
                    responseData.AddRange(v.Value.Count.ToByte());
                    foreach (var vv in v.Value.Values)
                        responseData.AddRange(vv.ToBinary());
                }

                var response = Header.Create(CODE, false, (ushort)responseData.Count);
                response.AddRange(responseData);

#pragma warning disable CS4014
                c.WriteAsync(response.ToArray());
#pragma warning restore CS4014
            }
            else
            {
                int offset = 0;
                int bidCount = d.ToInt(ref offset);
                for (int i = 0; i < bidCount; i++)
                {
                    var bid = NodeBid.FromBinary(d, ref offset);
                    var verified = await bid.VerifyAsync().ConfigureAwait(false);

                    if (!verified)
                    {
                        await ConnectionManager.RemoveAsync(c, "Invalid bid").ConfigureAwait(false);
                        return;
                    }

                    await Blockchain.AddToBidPoolAsync(bid).ConfigureAwait(false);
                }

                int voteCount = d.ToInt(ref offset);
                for (int i = 0; i < voteCount; i++)
                {
                    int count = d.ToInt(ref offset);
                    for (int j = 0; j < count; j++)
                    {
                        var vote = NodeVote.FromBinary(d, ref offset);
                        var verified = await vote.VerifyAsync().ConfigureAwait(false);
                        if (!verified)
                        {
                            await ConnectionManager.RemoveAsync(c, "Invalid vote").ConfigureAwait(false);
                            return;
                        }

                        await Blockchain.AddToVotePoolAsync(vote).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}