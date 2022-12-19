using System;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Cli.Prompts;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using Common;

namespace Node.CliCommands
{
    public class Transfer : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;

            string[] args = command.Split(' ');

            if (args.Length != 2)
                return CliHelper.Complete("Incorrect number of arguments"); 

            EthAddress address;
            if (!EthAddress.TryParse(args[0], out address))
                return CliHelper.Complete("Invalid argument"); 

            if (!BigDecimal.TryParse(args[1], Constants.DECIMALS, out BigDecimal decimalAmount))
                return CliHelper.Complete("Invalid argument"); 

            var amount = decimalAmount.ToAtomicUnits();

            var bal = await Blockchain.GetBalanceOfAsync(WalletStore.Current.Address).ConfigureAwait(false);
            var fee = Transaction.CalculateFee(Transaction_Type.Transfer);
            var decimalFee = fee.FromAtomicUnits();

            try
            {
                checked
                {
                    var required = amount + fee;
                    if (bal.Available == 0 || bal.Available < required)
                        return CliHelper.Complete($"Insufficient balance. {bal.Available.FromAtomicUnits()} available");
                }
            }
            catch
            {
                return CliHelper.Complete($"Transaction causes an arithmetic overflow");
            }

            CliHelper.Write($"Transaction Details{Environment.NewLine}", System.ConsoleColor.Green);
            CliHelper.Write($"    To: {address}");
            CliHelper.Write($"Amount: {decimalAmount.ToCurrencyString(Constants.DECIMALS)}");
            CliHelper.Write($"   Fee: {decimalFee.ToCurrencyString(Constants.DECIMALS)}");

            if (!QuestionPrompt.Get("Confirm send?", out QuestionPrompt_Response response))
                return CliHelper.Complete();

            if (response != QuestionPrompt_Response.Yes)
                return CliHelper.Complete();

            await Accounts.Transfer(address, amount).ConfigureAwait(false);
            
            return CliHelper.Complete();
        }
    }
}