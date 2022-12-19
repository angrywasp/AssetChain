using AngryWasp.Helpers;
using Newtonsoft.Json;
using System.Numerics;
using static AngryWasp.Helpers.ThreadSafeDictionary<string, Common.TransactionEntry>;

namespace Common
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TransactionEntry
    {
        [JsonProperty("i")]
        private uint chainId;

        [JsonProperty("h")]
        private string transactionHash;

        [JsonProperty("b")]
        private BigInteger blockNumber;

        [JsonProperty("t")]
        private ulong timestamp;

        [JsonProperty("s")]
        private bool success;

        public uint ChainId => chainId;
        public string TransactionHash => transactionHash.ToPrefixedHex();
        public ulong Timestamp => timestamp;
        public bool Success => success;

        public TransactionEntry() { }

        public TransactionEntry(uint chainId, string transactionHash, BigInteger blockNumber, ulong timestamp, bool success)
        {
            this.chainId = chainId;
            this.transactionHash = transactionHash;
            this.blockNumber = blockNumber;
            this.timestamp = timestamp;
            this.success = success;
        }

        public void UpdateFromReceipt(BigInteger blockNumber, bool success)
        {
            this.blockNumber = blockNumber;
            this.success = success;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class TransactionStore
    {
        public event ThreadSafeDictionaryEventHandler Added;
        public event ThreadSafeDictionaryEventHandler Removed;
        public event ThreadSafeDictionaryEventHandler Updated;

        [JsonProperty("d")]
        private ThreadSafeDictionary<string, TransactionEntry> store = new ThreadSafeDictionary<string, TransactionEntry>();

        public ThreadSafeDictionary<string, TransactionEntry> Store => store;

        public TransactionStore()
        {
            store.Added += (key, val) => Added?.Invoke(key, val);
            store.Updated += (key, val) => Updated?.Invoke(key, val);
            store.Removed += (key, val) => Removed?.Invoke(key, val);
        }
    }
}