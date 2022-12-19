using System;
using AngryWasp.Json.Rpc;
using System.Threading.Tasks;
using Node;
using AngryWasp.Cryptography;

namespace Server.RpcCommands
{
    [JsonRpcServerCommand("nonce")]
    public class Nonce : IJsonRpcServerCommand
    {
        public async Task<JsonRpcServerCommandResult> Handle(string requestString)
        {
            try
            {
                if (string.IsNullOrEmpty(requestString))
                    return Error.Generate("Empty request string", 10);

                if (!JsonRequest<EthAddress>.Deserialize(requestString, out JsonRequest<EthAddress> request))
                    return Error.Generate("Invalid JSON", 11);

                var nonce = await Blockchain.GetNextTxNonceAsync(request.Data).ConfigureAwait(false);
                return new JsonRpcServerCommandResult { Success = true, Value = nonce };
            }
            catch (Exception ex)
            {
                return Error.Generate($"Exception: {ex.Message}", 666);
            }
        }
    }
}