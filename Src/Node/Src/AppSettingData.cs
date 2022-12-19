using Common;
using Newtonsoft.Json;

namespace Node
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AppSettingData
    {
        [JsonProperty("p2pPort")]
        public ushort P2PPort { get; set; } = Constants.DEFAULT_P2P_PORT;

        [JsonProperty("rpcPort")]
        public ushort RpcPort { get; set; } = Constants.DEFAULT_RPC_PORT;
    }
}