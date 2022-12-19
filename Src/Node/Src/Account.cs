using System.Diagnostics;
using System.Threading.Tasks;
using AngryWasp.Cryptography;
using Common;
using Node.NetworkMessages;

namespace Node
{
    public static class Accounts
    {
        public static async Task Transfer(EthAddress to, ulong amount)
        {
            var bal = await Blockchain.GetBalanceOfAsync(WalletStore.Current.Address).ConfigureAwait(false);
            var fee = Transaction.CalculateFee(Transaction_Type.Transfer);

            checked
            {
                var required = amount + fee;
                if (bal.Available == 0 || bal.Available < required)
                return;
            };

            var nonce = await Blockchain.GetNextTxNonceAsync(WalletStore.Current.Address).ConfigureAwait(false);
            var tx = await Transaction.CreateTransfer(to, nonce, amount).ConfigureAwait(false);
            if (tx == null)
                return;
                
            await Blockchain.AddToTxPoolAsync(tx).ConfigureAwait(false);
            await MessageSender.BroadcastAsync(ShareTransactionNetworkCommand.GenerateRequest(tx)).ConfigureAwait(false);
        }

        public static async Task AddValidator()
        {
            var bal = await Blockchain.GetBalanceOfAsync(WalletStore.Current.Address).ConfigureAwait(false);
            var fee = Transaction.CalculateFee(Transaction_Type.AddValidator);

            checked
            {
                var required = Constants.VALIDATOR_STAKE + fee;
                if (bal.Available == 0 || bal.Available < required)
                return;
            };

            bool isValidator = await Blockchain.IsValidatorAsync(WalletStore.Current.Address, true).ConfigureAwait(false);

            if (isValidator)
                return;

            var nonce = await Blockchain.GetNextTxNonceAsync(WalletStore.Current.Address).ConfigureAwait(false);
            var tx = await Transaction.CreateAddValidator(nonce).ConfigureAwait(false);
            if (tx == null)
                return;

            await Blockchain.AddToTxPoolAsync(tx).ConfigureAwait(false);
            await MessageSender.BroadcastAsync(ShareTransactionNetworkCommand.GenerateRequest(tx)).ConfigureAwait(false);
        }

        public static async Task RemoveValidator()
        {
            var bal = await Blockchain.GetBalanceOfAsync(WalletStore.Current.Address).ConfigureAwait(false);
            var fee = Transaction.CalculateFee(Transaction_Type.RemoveValidator);

            checked
            {
                var required = Constants.VALIDATOR_STAKE + fee;
                if (bal.Available == 0 || bal.Available < required)
                return;
            };

            bool isValidator = await Blockchain.IsValidatorAsync(WalletStore.Current.Address, true).ConfigureAwait(false);

            if (!isValidator)
                return;

            var nonce = await Blockchain.GetNextTxNonceAsync(WalletStore.Current.Address).ConfigureAwait(false);
            var tx = await Transaction.CreateRemoveValidator(nonce).ConfigureAwait(false);
            if (tx == null)
                return;

            await Blockchain.AddToTxPoolAsync(tx).ConfigureAwait(false);
            await MessageSender.BroadcastAsync(ShareTransactionNetworkCommand.GenerateRequest(tx)).ConfigureAwait(false);
        }
    }
}