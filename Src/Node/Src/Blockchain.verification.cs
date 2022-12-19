using AngryWasp.Helpers;
using System;
using AngryWasp.Logger;
using System.Threading.Tasks;
using System.Collections.Generic;
using Common;

namespace Node
{
    public static partial class Blockchain
    {
        public static async Task<bool> Verify(this Block blk)
        {
            try
            {
                if (blk.Version != 0)
                {
                    Log.Instance.WriteError("Block version is invalid");
                    return false;
                }

                if (blk.Transactions.Count < Constants.TX_THRESHOLD)
                {
                    Log.Instance.WriteError("Not enough transactions in the block");
                    return false;
                }

                //make sure the block transactions are sorted before hashing
                blk.Transactions = blk.Transactions.SortTransactions();

                var hash = Block.GetHash(blk);
                if (hash != blk.Hash)
                {
                    Log.Instance.WriteError("Provided hash does not match calculated hash");
                    return false;
                }

                var recovered = blk.Signature.RecoverHashed(hash);

                if (recovered != blk.Validator)
                {
                    Log.Instance.WriteError("Invalid block. Not signed by validator.");
                    return false;
                }

                var isValidator = IsValidator(recovered, false);

                if (!isValidator)
                {
                    Log.Instance.WriteError("Invalid block. Not a registered validator.");
                    return false;
                }

                foreach (var v in blk.Sponsors)
                {
                    var voterIsValidator = IsValidator(v, false);
                    if (!voterIsValidator)
                    {
                        Log.Instance.WriteError("Invalid block. Sponsor not a registered validator.");
                        return false;
                    }

                    if (blk.Validator == v)
                    {
                        Log.Instance.WriteError("Invalid block. Validator sponsored their own block.");
                        return false;
                    }
                }

                var tasks = new List<Task<bool>>();

                foreach (var tx in blk.Transactions)
                    tasks.Add(Task.Run(async () => { return await tx.Verify().ConfigureAwait(false); }));

                await Task.WhenAll(tasks).ConfigureAwait(false);

                foreach (var t in tasks)
                {
                    bool verified = await t;
                    if (!verified)
                    {
                        Log.Instance.WriteError("Invalid block. One or more transactions in the block failed validation.");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Instance.WriteException(ex);
                return false;
            }
        }

        private static async Task<bool> Verify(this Transaction tx)
        {
            try
            {
                var hash = Transaction.GetHash(tx);
                if (hash != tx.Hash)
                {
                    Log.Instance.WriteError("Invalid transaction hash. Validation failed.");
                    return false;
                }

                if (tx.Fee < Transaction.CalculateFee(tx.Type))
                {
                    Log.Instance.WriteError("Underpriced transaction. Validation failed.");
                    return false;
                }

                try
                {
                    var recovered = tx.Signature.RecoverHashed(hash);

                    if (recovered != tx.From)
                    {
                        Log.Instance.WriteError("Invalid transaction signature. Validation failed.");
                        return false;
                    }
                }
                catch
                {
                    Log.Instance.WriteError("Failed to recover signer from transaction hash. Validation failed.");
                    return false;
                }

                var expectedDataSize = Transaction.GetDataSize(tx.Type);

                if (tx.Data.Length != expectedDataSize)
                {
                    Log.Instance.WriteError("Unexpected data size. Validation failed.");
                    return false;
                }

                bool dataValid = await VerifyTransactionData(tx).ConfigureAwait(false);
                return dataValid;
            }
            catch (Exception ex)
            {
                Log.Instance.WriteException(ex);
                return false;
            }
        }

#pragma warning disable CS1998

        private static async Task<bool> VerifyTransactionData(Transaction tx)
        {
            try
            {
                var bal = GetBalanceOf(tx.From, tx.Hash);
                
                switch (tx.Type)
                {
                    case Transaction_Type.Transfer:
                        {
                            try
                            {
                                checked
                                {
                                    var amount = tx.Data.ToULong();
                                    var total = amount + tx.Fee;
                                    return total <= bal.Available;
                                }
                            }
                            catch
                            {
                                return false;
                            }
                        }
                    case Transaction_Type.AddValidator:
                        {
                            bool isValidator = IsValidator(tx.From, true);
                            bool slotAvailable = registeredValidators.Count < Constants.MAX_VALIDATOR_SLOTS;
                            return !isValidator && slotAvailable && (Constants.VALIDATOR_STAKE + tx.Fee) <= bal.Available;
                        }
                    case Transaction_Type.RemoveValidator:
                        {
                            bool isValidator = IsValidator(tx.From, true);
                            return isValidator && tx.Fee <= bal.Available;
                        }
                    default:
                        Log.Instance.WriteWarning("Invalid transaction type. Validation failed.");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Log.Instance.WriteException(ex);
                return false;
            }
        }

#pragma warning restore CS1998

        private static bool Verify(this NodeBid bid)
        {
            var data = new List<byte>();
            data.AddRange(bid.LastBlockHash);
            data.AddRange(bid.Address);

            var recovered = bid.Signature.Recover(data.ToArray());
            if (recovered != bid.Address)
                return false;

            return true;
        }

        private static bool Verify(this NodeVote vote)
        {
            var data = new List<byte>();
            data.AddRange(vote.LastBlockHash);
            data.AddRange(vote.Address);
            data.AddRange(vote.VotedForAddress);

            var recovered = vote.Signature.Recover(data.ToArray());
            if (recovered != vote.Address)
                return false;

            if (vote.Address == vote.VotedForAddress)
                return false;

            return IsValidator(vote.Address, false);
        }

    }
}