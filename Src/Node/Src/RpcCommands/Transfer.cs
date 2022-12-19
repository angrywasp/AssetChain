using System;
using AngryWasp.Helpers;
using AngryWasp.Json.Rpc;
using System.Threading.Tasks;
using Node;
using Node.NetworkMessages;

namespace Server.RpcCommands
{
    [JsonRpcServerCommand("transfer")]
    public class Transfer : IJsonRpcServerCommand
    {
        public async Task<JsonRpcServerCommandResult> Handle(string requestString)
        {
            try
            {
                if (string.IsNullOrEmpty(requestString))
                    return Error.Generate("Empty request string", 10);

                JsonRequest<Transaction> request;
                if (!JsonRequest<Transaction>.Deserialize(requestString, out request))
                    return Error.Generate("Invalid JSON", 11);

                var tx = request.Data;

                if (tx == null)
                    return Error.Generate("Failed to deserialize JSON to transaction type", 12);

                var txVerified = await tx.VerifyAsync().ConfigureAwait(false);
                if (!txVerified)
                    return Error.Generate("Transaction failed verification", 20);

                var amount = tx.Data.ToULong();
                var bal = await Blockchain.GetBalanceOfAsync(tx.From).ConfigureAwait(false);
                var fee = Transaction.CalculateFee(Transaction_Type.Transfer);
                var decimalFee = fee.FromAtomicUnits();

                try
                {
                    checked
                    {
                        var required = amount + fee;
                        if (bal.Available == 0 || bal.Available < required)
                            return Error.Generate($"Insufficient balance. {bal.Available.FromAtomicUnits()} available", 31);
                    }
                }
                catch
                {
                    return Error.Generate($"Transaction causes an arithmetic overflow", 32);
                }

                if (!await Blockchain.AddToTxPoolAsync(tx).ConfigureAwait(false))
                    return Error.Generate($"Failed to add transaction to pool", 40);

                await MessageSender.BroadcastAsync(ShareTransactionNetworkCommand.GenerateRequest(tx)).ConfigureAwait(false);
                return new JsonRpcServerCommandResult { Success = true, Value = tx };
            }
            catch (Exception ex)
            {
                return Error.Generate($"Exception: {ex.Message}", 666);
            }
        }
    }
}