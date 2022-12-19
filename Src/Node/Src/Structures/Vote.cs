using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngryWasp.Cryptography;
using Common;
using Newtonsoft.Json;

namespace Node
{
    [JsonObject(MemberSerialization.OptIn)]
    public class NodeVote
    {
        [JsonProperty("lastBlockHash")]
        public HashKey32 LastBlockHash { get; set; }

        [JsonProperty("address")]
        public EthAddress Address { get; set; }

        [JsonProperty("votedForAddress")]
        public EthAddress VotedForAddress { get; set; }

        [JsonProperty("signature")]
        public DataSignature Signature { get; set; }

        public static NodeVote Create(BlockchainHead head, EthAddress address)
        {
            var vote = new NodeVote();

            vote.LastBlockHash = head.Block.Hash;
            vote.Address = WalletStore.Current.Address;
            vote.VotedForAddress = address;

            var data = new List<byte>();
            data.AddRange(vote.LastBlockHash);
            data.AddRange(vote.Address);
            data.AddRange(vote.VotedForAddress);

            vote.Signature = DataSignature.HashAndMake(data.ToArray(), WalletStore.Current.EcKey);
            return vote;
        }

        public List<byte> ToBinary()
        {
            var bin = new List<byte>();
            bin.AddRange(LastBlockHash);
            bin.AddRange(VotedForAddress);
            bin.AddRange(Address);
            bin.Add((byte)Signature.Count);
            bin.AddRange(Signature);
            return bin;
        }

        public static NodeVote FromBinary(byte[] bin, ref int offset)
        {
            var bid = new NodeVote();
            bid.LastBlockHash = bin.Skip(offset).Take(32).ToArray();
            offset += 32;
            bid.VotedForAddress = bin.Skip(offset).Take(20).ToArray();
            offset += 20;
            bid.Address = bin.Skip(offset).Take(20).ToArray();
            offset += 20;
            var sigLength = bin[offset];
            offset++;
            bid.Signature = bin.Skip(offset).Take(sigLength).ToArray();
            offset += sigLength;
            return bid;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        public static HashKey32 GetHash(NodeVote vote)
        {
            var data = new List<byte>();
            data.AddRange(vote.LastBlockHash);
            data.AddRange(vote.Address);
            data.AddRange(vote.VotedForAddress);
            return Keccak.Hash256(data.ToArray());
        }

        public static HashKey32 GetBidHash(NodeVote vote)
        {
            var data = new List<byte>();
            data.AddRange(vote.LastBlockHash);
            data.AddRange(vote.VotedForAddress);
            return Keccak.Hash256(data.ToArray());
        }
    }
}