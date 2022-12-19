using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using Nethereum.Signer;
using Newtonsoft.Json;

namespace Common
{
    public class DataSignatureJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(DataSignature);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            DataSignature hk = ((string)reader.Value).FromByteHex();
            return hk;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            DataSignature hk = (DataSignature)value;
            writer.WriteValue(hk.ToString());
        }
    }

    [JsonConverter(typeof(DataSignatureJsonConverter))]
    public struct DataSignature : IReadOnlyList<byte>, IEquatable<DataSignature>, IEquatable<byte[]>
    {
        private readonly byte[] value;

        public byte this[int index]
        {
            get
            {
                if (this.value != null)
                    return this.value[index];

                return default(byte);
            }
        }

        public HashKey32 R => value.Take(32).ToArray();
        public HashKey32 S => value.Skip(32).Take(32).ToArray();
        public byte[] V => value.Skip(64).ToArray();

        public int Count => value.Length;

        public static readonly DataSignature Empty = new byte[65]
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0
        };

        public bool IsNullOrEmpty()
        {
            if (value == null)
                return true;

            if (value.SequenceEqual(Empty))
                return true;

            return false;
        }

        public DataSignature(byte[] bytes)
        {
            value = bytes;
        }

        public static DataSignature HashAndMake(byte[] input, EthECKey privateKey)
        {
            HashKey32 hash = Keccak.Hash256(input);
            return Make(hash, privateKey);
        }

        public static DataSignature Make(HashKey32 hash, EthECKey key)
        {
            var sig = key.SignAndCalculateV(hash, Constants.CHAIN_ID);

            byte[] s = new byte[32];
            byte[] r = new byte[32];

            Buffer.BlockCopy(sig.S, 0, s, 32 - sig.S.Length, sig.S.Length);
            Buffer.BlockCopy(sig.R, 0, r, 32 - sig.R.Length, sig.R.Length);
            return new DataSignature(r.Concat(s).Concat(sig.V).ToArray());
        }

        public EthAddress Recover(byte[] data)
        {
            var sig = EthECDSASignatureFactory.FromComponents(value.Take(32).ToArray(), value.Skip(32).Take(32).ToArray(), value.Skip(64).ToArray());
            return EthECKey.RecoverFromSignature(sig, Keccak.Hash256(data), Constants.CHAIN_ID).GetPublicAddress();
        }

        public EthAddress RecoverHashed(HashKey32 hash)
        {
            var sig = EthECDSASignatureFactory.FromComponents(value.Take(32).ToArray(), value.Skip(32).Take(32).ToArray(), value.Skip(64).ToArray());
            return EthECKey.RecoverFromSignature(sig, hash, Constants.CHAIN_ID).GetPublicAddress();
        }

        public bool Equals(DataSignature other) => this.SequenceEqual(other);

        public bool Equals(byte[] other) => this.SequenceEqual(other);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            return obj is DataSignature && this.Equals((DataSignature)obj);
        }

        public IEnumerator<byte> GetEnumerator()
        {
            if (this.value != null)
                return ((IList<byte>)this.value).GetEnumerator();

            return Enumerable.Repeat(default(byte), 32).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public override int GetHashCode()
        {
            if (this.value == null)
                return 0;

            int offset = 0;
            return 
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) ^
                this.value.ToInt(ref offset) + this.value[64] + (this.value.Length == 65 ? 0 : this.value[65]);
        }

        public static bool operator ==(DataSignature left, DataSignature right) => left.Equals(right);

        public static bool operator !=(DataSignature left, DataSignature right) => !left.Equals(right);

        public static bool operator ==(byte[] left, DataSignature right) => right.Equals(left);

        public static bool operator !=(byte[] left, DataSignature right) => !right.Equals(left);

        public static bool operator ==(DataSignature left, byte[] right) => left.Equals(right);

        public static bool operator !=(DataSignature left, byte[] right) => !left.Equals(right);

        public static implicit operator DataSignature(byte[] value) => new DataSignature(value);

        public static implicit operator byte[](DataSignature value) => value.ToByte();

        public static implicit operator List<byte>(DataSignature value) => value.ToList();

        public static implicit operator DataSignature(List<byte> value) => new DataSignature(value.ToArray());

        public static implicit operator DataSignature(string hex) => new DataSignature(hex.FromByteHex());
        
        public override string ToString() => value.ToHex();

        public byte[] ToByte() => value;
    }
}