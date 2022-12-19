using AngryWasp.Json.Rpc;
using AngryWasp.Logger;

namespace Server.RpcCommands
{
    public class Error
    {
        public static JsonRpcServerCommandResult Generate(string errorMessage, uint code)
        {
            JsonResponse<object> response = new JsonResponse<object>();
            response.Data = errorMessage;
            response.ErrorCode = code;

            Log.Instance.WriteError($"RPC Error {code}: {errorMessage}");

            return new JsonRpcServerCommandResult { Success = false, Value = response };
        }
    }
}