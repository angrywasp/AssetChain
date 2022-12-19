using System;
using System.IO;
using System.Threading.Tasks;
using AngryWasp.Cryptography;
using AngryWasp.Logger;
using Common;
using Newtonsoft.Json;

namespace RpcClient
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AppSettingData
    {
        [JsonProperty("rpcHost")]
        public string RpcHost { get; set; }

        [JsonProperty("rpcPort")]
        public ushort RpcPort { get; set; }
    }
}