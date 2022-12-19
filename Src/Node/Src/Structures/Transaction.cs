using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Logger;
using Common;
using Newtonsoft.Json;

namespace Node
{
    //Data sizes are mapped to the type
    public enum Transaction_Type : byte
    {
        Invalid,
        Transfer,
        AddValidator,
        RemoveValidator,
        Max
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Transaction
    {
        [JsonProperty("version")]
        public ushort Version { get; set; } = 0;

        [JsonProperty("type")]
        public Transaction_Type Type { get; set; } = Transaction_Type.Invalid;

        [JsonProperty("fee")]
        public ulong Fee { get; set; } = 0;

        [JsonProperty("nonce")]
        public uint Nonce { get; set; } = 0;

        [JsonProperty("from")]
        public EthAddress From { get; set; } = EthAddress.Empty;

        [JsonProperty("to")]
        public EthAddress To { get; set; } = EthAddress.Empty;

        [JsonProperty("data")]
        [JsonConverter(typeof(ByteArrayJsonConverter))]
        public byte[] Data { get; set; }

        [JsonProperty("hash")]
        public HashKey32 Hash { get; set; } = HashKey32.Empty;

        [JsonProperty("signature")]
        public DataSignature Signature { get; set; } = DataSignature.Empty;

        public static async Task<Transaction> CreateTransfer(EthAddress to, uint nonce, ulong amount)
        {
            var tx = new Transaction(Transaction_Type.Transfer, nonce, to, amount.ToByte());
            bool verified = await tx.VerifyAsync().ConfigureAwait(false);

            if (!verified)
                return null;

            return tx;
        }

        public static async Task<Transaction> CreateAddValidator(uint nonce)
        {
            var tx = new Transaction(Transaction_Type.AddValidator, nonce, EthAddress.Empty, new byte[0]);
            bool verified = await tx.VerifyAsync().ConfigureAwait(false);

            if (!verified)
                return null;

            return tx;
        }

        public static async Task<Transaction> CreateRemoveValidator(uint nonce)
        {
            var tx = new Transaction(Transaction_Type.RemoveValidator, nonce, EthAddress.Empty, new byte[0]);
            bool verified = await tx.VerifyAsync().ConfigureAwait(false);

            if (!verified)
                return null;

            return tx;
        }

        public Transaction() { }

        public Transaction(Transaction_Type type, uint nonce, EthAddress to, byte[] data)
        {
            this.Version = 0;
            this.Type = type;
            this.Nonce = nonce;
            this.From = WalletStore.Current.Address;
            this.To = to;
            this.Data = data;
            this.Fee = CalculateFee(type);
            this.Hash = GetHash(this);
            this.Signature = DataSignature.Make(this.Hash, WalletStore.Current.EcKey);
        }

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
        
        public static ushort GetDataSize(Transaction_Type txType)
        {
            switch (txType)
            {
                case Transaction_Type.Transfer:
                    return 8;
                case Transaction_Type.AddValidator:
                case Transaction_Type.RemoveValidator:
                    return 0;
                default:
                    return ushort.MaxValue;
            }
        }

        public static ulong CalculateFee(Transaction_Type txType)
        {
            switch (txType)
            {
                case Transaction_Type.Transfer:
                    return 100;
                case Transaction_Type.AddValidator:
                case Transaction_Type.RemoveValidator:
                    return 500;
                default:
                    Log.Instance.WriteWarning("Invalid transaction type. Fee calculation failed.");
                    return ulong.MaxValue;
            }
        }

        public static HashKey32 GetHash(Transaction tx)
        {
            var data = new List<byte>();
            data.AddRange(tx.Version.ToByte());
            data.Add((byte)tx.Type);
            data.AddRange(tx.Nonce.ToByte());
            data.AddRange(tx.From);
            data.AddRange(tx.To);
            data.AddRange(tx.Data);
            data.AddRange(tx.Fee.ToByte());
            return Keccak.Hash256(data.ToArray());
        }

        public List<byte> ToBinary()
        {
            List<byte> bin = new List<byte>();
            bin.AddRange(Version.ToByte());
            bin.Add((byte)Type);
            bin.AddRange(Nonce.ToByte());
            bin.AddRange(From);
            bin.AddRange(To);
            bin.AddRange(Data);
            bin.AddRange(Fee.ToByte());
            bin.AddRange(Hash);
            bin.Add((byte)Signature.Count);
            bin.AddRange(Signature);
            return bin;
        }

        public static bool FromBinary(byte[] bin, ref int offset, out Transaction tx)
        {
            var txType = (Transaction_Type)bin[offset + 2];

            ushort dataLength = GetDataSize(txType);

            var expectedDataSize = 144 + dataLength;

            if ((offset + expectedDataSize) > bin.Length)
            {
                Log.Instance.WriteWarning("Insufficient data to parse transaction");
                tx = null;
                return false;
            }

            tx = new Transaction();

            tx.Version = bin.ToUShort(ref offset);
            tx.Type = (Transaction_Type)bin[offset++];
            tx.Nonce = bin.ToUInt(ref offset);

            tx.From = bin.Skip(offset).Take(20).ToArray();
            offset += 20;

            tx.To = bin.Skip(offset).Take(20).ToArray();
            offset += 20;

            if (dataLength > 0)
            {
                tx.Data = bin.Skip(offset).Take(dataLength).ToArray();
                offset += dataLength;
            }
            else
                tx.Data = new byte[0];

            tx.Fee = bin.ToULong(ref offset);

            tx.Hash = bin.Skip(offset).Take(32).ToArray();
            offset += 32;

            var sigLength = bin[offset];
            offset++;

            tx.Signature = bin.Skip(offset).Take(sigLength).ToArray();
            offset += sigLength;
            return true;
        }
    }
}