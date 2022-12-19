using System.Collections.Generic;
using System.Linq;
using AngryWasp.Cryptography;
using Common;
using Newtonsoft.Json;

namespace Node
{
    [JsonObject(MemberSerialization.OptIn)]
    public class NodeBid
    {
        [JsonProperty("lastBlockHash")]
        public HashKey32 LastBlockHash { get; set; }

        [JsonProperty("address")]
        public EthAddress Address { get; set; }

        [JsonProperty("signature")]
        public DataSignature Signature { get; set; }

        public static NodeBid Create(BlockchainHead head)
        {
            if (head == null)
                return null;
                
            var bid = new NodeBid();
 
            bid.LastBlockHash = head.Block.Hash;
            bid.Address = WalletStore.Current.Address;

            var data = new List<byte>();
            data.AddRange(bid.LastBlockHash);
            data.AddRange(bid.Address);

            bid.Signature = DataSignature.HashAndMake(data.ToArray(), WalletStore.Current.EcKey);
            return bid;
        }

        public List<byte> ToBinary()
        {
            var bin = new List<byte>();
            bin.AddRange(LastBlockHash);
            bin.AddRange(Address);
            bin.Add((byte)Signature.Count);
            bin.AddRange(Signature);

            return bin;
        }

        public static NodeBid FromBinary(byte[] bin, ref int offset)
        {
            var bid = new NodeBid();
            bid.LastBlockHash = bin.Skip(offset).Take(32).ToArray();
            offset += 32;
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

        public static HashKey32 GetHash(NodeBid bid)
        {
            var data = new List<byte>();
            data.AddRange(bid.LastBlockHash);
            data.AddRange(bid.Address);
            return Keccak.Hash256(data.ToArray());
        }
    }
}