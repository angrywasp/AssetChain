using System;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Cli.Prompts;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Json.Rpc;
using Common;
using Newtonsoft.Json;

namespace RpcClient.CliCommands
{
    public class Transfer : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;

            string[] args = command.Split(' ');

            if (args.Length != 2)
                return CliHelper.Complete("Incorrect number of arguments"); 

            if (!EthAddress.TryParse(args[0], out EthAddress address))
                return CliHelper.Complete("Invalid argument"); 

            if (!BigDecimal.TryParse(args[1], Constants.DECIMALS, out BigDecimal decimalAmount))
                return CliHelper.Complete("Invalid argument"); 

            var amount = decimalAmount.ToAtomicUnits();

            var fee = Transaction.CalculateFee(Transaction_Type.Transfer);
            var decimalFee = fee.FromAtomicUnits();

            CliHelper.Write($"Transaction Details{Environment.NewLine}", System.ConsoleColor.Green);
            CliHelper.Write($"    To: {address}");
            CliHelper.Write($"Amount: {decimalAmount.ToCurrencyString(Constants.DECIMALS)}");
            CliHelper.Write($"   Fee: {decimalFee.ToCurrencyString(Constants.DECIMALS)}");

            if (!QuestionPrompt.Get("Confirm send?", out QuestionPrompt_Response response))
                return CliHelper.Complete();

            if (response != QuestionPrompt_Response.Yes)
                return CliHelper.Complete();

            var client = new JsonRpcClient($"http://{Program.Settings.AppData.RpcHost}", Program.Settings.AppData.RpcPort);

            var br = await client.SendRequest("balance", new JsonRequest<EthAddress>() {
                Data = WalletStore.Current.Address
            }.Serialize()).ConfigureAwait(false);
            
            var balance = JsonConvert.DeserializeObject<AccountBalance>(br);

            if (amount + fee > balance.Available)
                return CliHelper.Complete($"Insufficient balance. {balance.Available.FromAtomicUnits()} available");

            var nr = await client.SendRequest("nonce", new JsonRequest<EthAddress>() {
                Data = address
            }.Serialize()).ConfigureAwait(false);

            var nonce = JsonConvert.DeserializeObject<uint>(nr);

            var tr = await client.SendRequest("transfer", new JsonRequest<Transaction>() {
                Data = Transaction.CreateTransfer(address, nonce, amount)
            }.Serialize()).ConfigureAwait(false);

            var submittedTx = JsonConvert.DeserializeObject<Transaction>(tr);
            CliHelper.Write(submittedTx.ToString());

            return CliHelper.Complete();
        }
    }
}