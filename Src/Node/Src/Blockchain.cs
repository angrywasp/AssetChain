using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Cryptography;
using AngryWasp.Helpers;
using AngryWasp.Logger;
using AngryWasp.Net;
using Common;
using Node.NetworkMessages;

namespace Node
{
    public static partial class Blockchain
    {
        private static AsyncLock asyncLock = new AsyncLock();

        public static async Task<List<EthAddress>> GetValidatorList()
        {
            using (await asyncLock.LockAsync())
                return registeredValidators.ToList();
        }

        public static async Task<bool> AddToBlockchainAsync(Block blk)
        {
            using (await asyncLock.LockAsync())
                return await AddToBlockchain(blk).ConfigureAwait(false);
        }

        public static async Task<bool> IsNextBlockAsync(Block blk)
        {
            using (await asyncLock.LockAsync())
                return IsNextBlock(blk);
        }

        public static async Task<Dictionary<int, Block>> GetBlockPoolAsync()
        {
            using (await asyncLock.LockAsync())
                return new Dictionary<int, Block>(blockPool.ToArray());
        }

        public static async Task<Dictionary<HashKey32, Transaction>> GetTransactionPoolAsync()
        {
            using (await asyncLock.LockAsync())
            {
                CleanTxPool();
                return new Dictionary<HashKey32, Transaction>(txPool.ToArray());
            }
        }

        public static async Task<Dictionary<HashKey32, NodeBid>> GetBidPoolAsync()
        {
            using (await asyncLock.LockAsync())
            {
                CleanVotingPool();
                return new Dictionary<HashKey32, NodeBid>(bidPool.ToArray());
            }
        }

        public static async Task<Dictionary<HashKey32, Dictionary<HashKey32, NodeVote>>> GetVotePoolAsync()
        {
            using (await asyncLock.LockAsync())
            {
                CleanVotingPool();
                return new Dictionary<HashKey32, Dictionary<HashKey32, NodeVote>>(votePool.ToArray());
            }
        }

        public static async Task<bool> AddToTxPoolAsync(Transaction tx)
        {
            using (await asyncLock.LockAsync())
                return AddToTxPool(tx);
        }

        public static async Task<bool> IsValidatorAsync(EthAddress address, bool ignoreAge)
        {
            using (await asyncLock.LockAsync())
                return IsValidator(address, ignoreAge);
        }

        public static async Task<bool> AddToBidPoolAsync(NodeBid bid)
        {
            using (await asyncLock.LockAsync())
                return AddToBidPool(bid);
        }

        public static async Task<bool> AddToVotePoolAsync(NodeVote vote)
        {
            using (await asyncLock.LockAsync())
                return AddToVotePool(vote);
        }

        public static async Task<bool> HasVotedAsync(HashKey32 lastBlockHash, EthAddress address)
        {
            using (await asyncLock.LockAsync())
                return HasVoted(lastBlockHash, address);
        }

        public static async Task<BlockchainHead> GetHeadAsync()
        {
            using (await asyncLock.LockAsync())
                return GetHead();
        }

        public static async Task<List<(NodeBid Bid, HashKey32 Hash, ulong Weight)>> GetSortedBidListAsync()
        {
            using (await asyncLock.LockAsync())
                return GetSortedBidList();
        }

        public static async Task<bool> GetIsSynchronizedAsync()
        {
            using (await asyncLock.LockAsync())
                return isSynchronized;
        }

        public static async Task SetIsSynchronizedAsync(bool value)
        {
            using (await asyncLock.LockAsync())
                isSynchronized = value;
        }

        public static async Task<Block> GetBlockAsync(int index)
        {
            using (await asyncLock.LockAsync())
                return GetBlock(index);
        }

        public static async Task<List<Block>> GetBlocksAsync(int index, int count)
        {
            using (await asyncLock.LockAsync())
                return GetBlocks(index, count);
        }

        public static async Task<uint> GetNextTxNonceAsync(EthAddress address)
        {
            using (await asyncLock.LockAsync())
                return GetNextTxNonce(address);
        }

        public static async Task<Block> GetBlockAsync(HashKey32 hash)
        {
            using (await asyncLock.LockAsync())
                return GetBlock(hash);
        }

        public static async Task<Transaction> GetTransactionAsync(HashKey32 hash)
        {
            using (await asyncLock.LockAsync())
                return GetTransaction(hash);
        }

        public static async Task<AccountBalance> GetBalanceOfAsync(EthAddress address, HashKey32? exclude = null)
        {
            using (await asyncLock.LockAsync())
                return GetBalanceOf(address, exclude);
        }

        public static async Task<ulong> GetCurrentBalanceOfAsync(EthAddress address)
        {
            using (await asyncLock.LockAsync())
                return GetCurrentBalanceOf(address);
        }

        public static async Task<AccountState> GetAccountAsync(EthAddress address)
        {
            using (await asyncLock.LockAsync())
                return GetAccount(address);
        }

        public static async Task CleanPoolsAsync()
        {
            using (await asyncLock.LockAsync())
            {
                CleanTxPool();
                CleanVotingPool();
            }
        }

        public static async Task<bool> EvaluateBalanceTransferStateAsync(List<Transaction> txs)
        {
            using (await asyncLock.LockAsync())
                return EvaluateBalanceTransferState(txs);
        }

        public static async Task<bool> EvaluateTransactionNoncesAsync(List<Transaction> txs)
        {
            using (await asyncLock.LockAsync())
                return EvaluateTransactionNonces(txs);
        }

        public static async Task<(bool, NodeBid Bid, HashKey32 Hash, ulong Weight, List<NodeVote> Votes)> CheckConsensusAsync()
        {
            using (await asyncLock.LockAsync())
            {
                BroadcastBid();

                if (txPool.Count >= Constants.TX_THRESHOLD)
                    BroadcastVote(WalletStore.Current.Address);

                var result = GetConsensus();

                if (result.Ok)
                {
                    ApplicationLogWriter.WriteBuffered($"Consensus OK. {result.Bid.Address}{Environment.NewLine}", ConsoleColor.Cyan);
                    if (result.Bid.Address == WalletStore.Current.Address)
                    {
                        var transactions = GetSortedTransactions();
                        if (transactions.Count >= Constants.TX_THRESHOLD)
                        {
                            var newBlock = Block.Create(GetHead(), transactions, result.Votes.Select(x => x.Address).ToList());
                            bool verified = await newBlock.Block.Verify().ConfigureAwait(false);
                            if (!verified)
                            {
                                Log.Instance.WriteError("Bid failed sanity check");
                                return result;
                            }

                            var addedToBlockchain = await AddToBlockchain(newBlock.Block).ConfigureAwait(false);
                            if (!addedToBlockchain)
                            {
                                Log.Instance.WriteError("Failed to add new block to the chain. Potential internal error.");
                                return result;
                            }

#pragma warning disable CS4014
                            MessageSender.BroadcastAsync(ShareBlockNetworkCommand.GenerateRequest(newBlock.Index, newBlock.Block));
#pragma warning restore CS4014   
                        }
                    }
                }

                return result;
            }
        }

        public static async Task InitiateSyncAsync(int expectedResponses)
        {
            using (await asyncLock.LockAsync())
                InitiateSync(expectedResponses);
        }

        public static async Task<ulong> CalculateNodeWeightAsync(EthAddress address)
        {
            using (await asyncLock.LockAsync())
                return CalculateNodeWeight(address);
        }

        public static async Task<(int NewHeight, int RequiredBlocks)> ProcessSyncResponseAsync(int responseIndex, HashKey32 responseHash, Connection c, int threshold = 0)
        {
            using (await asyncLock.LockAsync())
                return ProcessSyncResponse(responseIndex, responseHash, c, threshold);
        }

        public static async Task<bool?> HandleIncomingBlockAsync(int index, Block blk)
        {
            using (await asyncLock.LockAsync())
                return await HandleIncomingBlock(index, blk).ConfigureAwait(false);
        }

        public static async Task<bool> HandleIncomingBlocksAsync(int index, List<Block> blks)
        {
            using (await asyncLock.LockAsync())
                return await HandleIncomingBlocks(index, blks).ConfigureAwait(false);
        }

        public static async Task<bool> VerifyAsync(this Transaction tx)
        {
            using (await asyncLock.LockAsync())
                return await tx.Verify().ConfigureAwait(false);
        }

        public static async Task<bool> VerifyAsync(this NodeBid bid)
        {
            using (await asyncLock.LockAsync())
                return bid.Verify();
        }

        public static async Task<bool> VerifyAsync(this NodeVote vote)
        {
            using (await asyncLock.LockAsync())
                return vote.Verify();
        }
    }
}