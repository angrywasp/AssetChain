using Newtonsoft.Json;

namespace Common
{
    [JsonObject(MemberSerialization.OptIn)]
    public class WalletDataStore
    {
        [JsonProperty("tx")]
        public TransactionStore TransactionStore { get; set; } = new TransactionStore();
    }
}