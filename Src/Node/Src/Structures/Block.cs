using System.Collections.Generic;
using System.Linq;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Logger;
using Common;
using Newtonsoft.Json;

namespace Node
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Block
    {
        [JsonProperty("version")]
        public ushort Version { get; set; } = 0;

        [JsonProperty("timestamp")]
        public ulong Timestamp { get; set; } = 0;

        [JsonProperty("lastHash")]
        public HashKey32 LastHash { get; set; } = HashKey32.Empty;

        [JsonProperty("validator")]
        public EthAddress Validator { get; set; } = EthAddress.Empty;

        [JsonProperty("transactions")]
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();

        [JsonProperty("sponsors")]
        public List<EthAddress> Sponsors { get; set; } = new List<EthAddress>();

        [JsonProperty("hash")]
        public HashKey32 Hash { get; set; } = HashKey32.Empty;

        [JsonProperty("signature")]
        public DataSignature Signature { get; set; } = DataSignature.Empty;

        public static Block Genesis()
        {
            uint nonce = 0;
            var txs = new List<Transaction>();
            for (var i = 0; i < Constants.GENESIS_VALIDATORS.Length; i++)
            {
                {
                    var tx = new Transaction
                    {
                        Type = Transaction_Type.Transfer,
                        From = EthAddress.Empty,
                        To = Constants.GENESIS_VALIDATORS[i],
                        Nonce = nonce++,
                        Data = Constants.TOTAL_SUPPLY.ToByte(),
                        Fee = Transaction.CalculateFee(Transaction_Type.Transfer)
                    };

                    tx.Hash = Transaction.GetHash(tx);
                    txs.Add(tx);
                }

                {
                    var tx = new Transaction
                    {
                        Type = Transaction_Type.AddValidator,
                        From = Constants.GENESIS_VALIDATORS[i],
                        To = EthAddress.Empty,
                        Nonce = 0,
                        Data = new byte[0],
                        Fee = Transaction.CalculateFee(Transaction_Type.AddValidator)
                    };

                    tx.Hash = Transaction.GetHash(tx);
                    txs.Add(tx);
                }
            }

            var blk = new Block
            {
                LastHash = HashKey32.Empty,
                Validator = EthAddress.Empty,
                Timestamp = 0,
                Transactions = txs.SortTransactions()
            };

            blk.Hash = GetHash(blk);
            blk.Signature = DataSignature.Make(blk.Hash, WalletStore.Current.EcKey);

            return blk;
        }

        public static Block CreateGenesisBlock(EthAddress validator, HashKey32 prevHash)
        {
            Block blk = new Block
            {
                LastHash = prevHash,
                Validator = validator
            };

            blk.Hash = GetHash(blk);
            return blk;
        }

        public static (Block Block, int Index) Create(BlockchainHead head, List<Transaction> transactions, List<EthAddress> voters)
        {
            var blk = new Block();

            blk.Version = 0;
            blk.Timestamp = DateTimeHelper.TimestampNow;
            blk.LastHash = head.Block.Hash;
            blk.Transactions = transactions.SortTransactions();
            blk.Validator = WalletStore.Current.Address;
            blk.Sponsors = voters;

            blk.Hash = GetHash(blk);
            blk.Signature = DataSignature.Make(blk.Hash, WalletStore.Current.EcKey);

            return (blk, head.Index + 1);
        }

        public static HashKey32 GetHash(Block blk)
        {
            var data = new List<byte>();
            data.AddRange(blk.Version.ToByte());
            data.AddRange(blk.Timestamp.ToByte());
            data.AddRange(blk.LastHash);
            data.AddRange(blk.Validator);

            foreach (var tx in blk.Transactions)
                data.AddRange(tx.ToBinary());

            foreach (var v in blk.Sponsors)
                data.AddRange(v);

            return Keccak.Hash256(data.ToArray());
        }

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);

        public List<byte> ToBinary()
        {
            List<byte> bin = new List<byte>();
            bin.AddRange(Version.ToByte());
            bin.AddRange(Timestamp.ToByte());
            bin.AddRange(LastHash);
            bin.AddRange(Hash);
            bin.AddRange(Validator);
            bin.Add((byte)Signature.Count);
            bin.AddRange(Signature);

            bin.AddRange(Transactions.Count.ToByte());
            foreach (var tx in Transactions)
                bin.AddRange(tx.ToBinary());

            bin.AddRange(Sponsors.Count.ToByte());
            foreach (var v in Sponsors)
                bin.AddRange(v);

            return bin;
        }

        public static bool FromBinary(byte[] bin, ref int offset, out Block blk)
        {
            var expectedDataSize = 167; //minimum size without transactions and voters

            if ((offset + expectedDataSize) > bin.Length)
            {
                Log.Instance.WriteWarning("Insufficient data to parse block");
                blk = null;
                return false;
            }

            blk = new Block();

            blk.Version = bin.ToUShort(ref offset);
            blk.Timestamp = bin.ToULong(ref offset);

            blk.LastHash = bin.Skip(offset).Take(32).ToArray();
            offset += 32;

            blk.Hash = bin.Skip(offset).Take(32).ToArray();
            offset += 32;

            blk.Validator = bin.Skip(offset).Take(20).ToArray();
            offset += 20;

            var sigLength = bin[offset];
            offset++;

            blk.Signature = bin.Skip(offset).Take(sigLength).ToArray();
            offset += sigLength;

            int txCount = bin.ToInt(ref offset);

            for (int i = 0; i < txCount; i++)
            {
                Transaction tx;
                if (!Transaction.FromBinary(bin, ref offset, out tx))
                {
                    Log.Instance.WriteWarning("Failed to parse transaction in block");
                    blk = null;
                    return false;
                }
                blk.Transactions.Add(tx);
            }

            int vCount = bin.ToInt(ref offset);
            for (int i = 0; i < vCount; i++)
            {
                blk.Sponsors.Add(bin.Skip(offset).Take(20).ToArray());
                offset += 20;
            }

            return true;
        }
    }
}