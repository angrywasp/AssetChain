using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AngryWasp.Helpers;
using Common;
using Newtonsoft.Json;
using JsonConverter = Newtonsoft.Json.JsonConverter;

namespace Node
{
    public static class Helpers
    {
        public static async Task ConnectToPeerList()
        {
            var nodes = Database.SelectMostRecentPeers(10);
            await AngryWasp.Net.Client.ConnectToNodeList(nodes).ConfigureAwait(false);
        }

        public static BigDecimal FromAtomicUnits(this ulong value) => BigDecimal.Create(value, Constants.DECIMALS);

        public static ulong ToAtomicUnits(this BigDecimal value) => (ulong)value.Mantissa;
    }

    public class ByteArrayJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(byte[]);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return ((string)reader.Value).FromByteHex();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((byte[])value).ToHex());
        }
    }
}