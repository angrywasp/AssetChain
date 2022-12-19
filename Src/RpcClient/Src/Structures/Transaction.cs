using System.Collections.Generic;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Logger;
using Common;
using Newtonsoft.Json;

namespace RpcClient
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

    public class Transaction
    {
        public ushort Version { get; set; } = 0;
        public Transaction_Type Type { get; set; } = Transaction_Type.Invalid;
        public ulong Fee { get; set; } = 0;
        public uint Nonce { get; set; } = 0;
        public EthAddress From { get; set; } = EthAddress.Empty;
        public EthAddress To { get; set; } = EthAddress.Empty;
        public byte[] Data { get; set; }
        public HashKey32 Hash { get; set; } = HashKey32.Empty;
        public DataSignature Signature { get; set; } = DataSignature.Empty;

        public static Transaction CreateTransfer(EthAddress to, uint nonce, ulong amount) =>
            Transaction.Create(Transaction_Type.Transfer, nonce, to, amount.ToByte());

        public static Transaction CreateAddValidator(uint nonce) =>
            Transaction.Create(Transaction_Type.AddValidator, nonce, EthAddress.Empty, new byte[0]);

        public static Transaction CreateRemoveValidator(uint nonce) =>
            Transaction.Create(Transaction_Type.RemoveValidator, nonce, EthAddress.Empty, new byte[0]);


        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);

        public static Transaction Create(Transaction_Type type, uint nonce, EthAddress to, byte[] data)
        {
            var tx = new Transaction();

            tx.Version = 0;
            tx.Type = type;
            tx.Nonce = nonce;
            tx.From = WalletStore.Current.Address;
            tx.To = to;
            tx.Data = data;
            tx.Fee = CalculateFee(type);

            tx.Hash = Keccak.Hash256(GetData(tx));
            tx.Signature = DataSignature.Make(tx.Hash, WalletStore.Current.EcKey);
            return tx;
        }

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

        public static byte[] GetData(Transaction tx)
        {
            var data = new List<byte>();
            data.AddRange(tx.Version.ToByte());
            data.Add((byte)tx.Type);
            data.AddRange(tx.Nonce.ToByte());
            data.AddRange(tx.From);
            data.AddRange(tx.To);
            data.AddRange(tx.Data);

            return data.ToArray();
        }
    }
}