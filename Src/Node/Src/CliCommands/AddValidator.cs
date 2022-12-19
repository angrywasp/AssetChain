using System;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Cli.Prompts;
using AngryWasp.Helpers;
using Common;

namespace Node.CliCommands
{
    public class AddValidator : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            if (!CliHelper.Begin()) return false;

            var bal = await Blockchain.GetBalanceOfAsync(WalletStore.Current.Address).ConfigureAwait(false);
            var fee = Transaction.CalculateFee(Transaction_Type.AddValidator);
            var decimalFee = fee.FromAtomicUnits();

            checked
            {
                var required = Constants.VALIDATOR_STAKE + fee;
                if (bal.Available == 0 || bal.Available < required)
                    return CliHelper.Complete($"Insufficient balance. {bal.Available.FromAtomicUnits()} available");
            }

            if (await Blockchain.IsValidatorAsync(WalletStore.Current.Address, true).ConfigureAwait(false))
                return CliHelper.Complete($"Already registered as a validator");

            CliHelper.Write($"Transaction Details{Environment.NewLine}", System.ConsoleColor.Green);
            CliHelper.Write($"Cost: {Constants.ValidatorStake.ToCurrencyString(Constants.DECIMALS)}");
            CliHelper.Write($" Fee: {decimalFee.ToCurrencyString(Constants.DECIMALS)}");

            if (!QuestionPrompt.Get("Confirm registration?", out QuestionPrompt_Response response))
                return CliHelper.Complete();

            if (response != QuestionPrompt_Response.Yes)
                return CliHelper.Complete();

            await Accounts.AddValidator().ConfigureAwait(false);
            
            return CliHelper.Complete();
        }
    }
}