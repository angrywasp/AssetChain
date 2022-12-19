using System;
using AngryWasp.Json.Rpc;
using System.Threading.Tasks;
using Node;
using AngryWasp.Cryptography;

namespace Server.RpcCommands
{
    [JsonRpcServerCommand("balance")]
    public class Balance : IJsonRpcServerCommand
    {
        public async Task<JsonRpcServerCommandResult> Handle(string requestString)
        {
            try
            {
                if (string.IsNullOrEmpty(requestString))
                    return Error.Generate("Empty request string", 10);

                if (!JsonRequest<EthAddress>.Deserialize(requestString, out JsonRequest<EthAddress> request))
                    return Error.Generate("Invalid JSON", 11);

                var account = await Blockchain.GetBalanceOfAsync(request.Data).ConfigureAwait(false);
                return new JsonRpcServerCommandResult { Success = true, Value = account };
            }
            catch (Exception ex)
            {
                return Error.Generate($"Exception: {ex.Message}", 666);
            }
        }
    }
}