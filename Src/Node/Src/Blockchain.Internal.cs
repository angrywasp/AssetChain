using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class BlockchainHead
    {
        public int Index { get; set; }
        public Block Block { get; set; }

        public override string ToString()
        {
            return $"{Block.Hash}, Index: {Index}";
        }
    }

    public class AccountState
    {
        public ulong Balance { get; set; } = 0;
        public uint Nonce { get; set; } = 0;
    }

    public class AccountBalance
    {
        public ulong Current { get; set; } = 0;
        public ulong Available { get; set; } = 0;

        public override string ToString()
        {
            return $"Current: {Current}, Available: {Available}";
        }
    }

    public class SyncState
    {
        public Dictionary<int, List<(HashKey32, Connection)>> Responses { get; set; } =
            new Dictionary<int, List<(HashKey32, Connection)>>();
        public int ExpectedResponses { get; set; }
        public int TotalResponses { get; set; }
        public int LocalIndex { get; set; }
        public HashKey32 LocalHash { get; set; }
    }

    public static partial class Blockchain
    {
        private static Dictionary<int, Block> blockPool = new Dictionary<int, Block>();
        private static Dictionary<HashKey32, Transaction> txPool = new Dictionary<HashKey32, Transaction>();
        private static Dictionary<HashKey32, NodeBid> bidPool = new Dictionary<HashKey32, NodeBid>();
        private static Dictionary<HashKey32, Dictionary<HashKey32, NodeVote>> votePool =
            new Dictionary<HashKey32, Dictionary<HashKey32, NodeVote>>();

        private static List<Block> blocks;
        private static Dictionary<HashKey32, ulong> blockHashes;
        private static Dictionary<EthAddress, AccountState> accounts;
        private static HashSet<EthAddress> registeredValidators;
        private static Dictionary<HashKey32, Transaction> transactions;
        private static Dictionary<EthAddress, ulong> lastBlockHeights;
        private static Dictionary<EthAddress, ulong> validatorAges;
        private static bool isSynchronized = false;

        private static SyncState syncState;

        public static async Task Load()
        {
            blocks = new List<Block>();
            blockHashes = new Dictionary<HashKey32, ulong>();
            accounts = new Dictionary<EthAddress, AccountState>();
            registeredValidators = new HashSet<EthAddress>();
            validatorAges = new Dictionary<EthAddress, ulong>();
            transactions = new Dictionary<HashKey32, Transaction>();
            lastBlockHeights = new Dictionary<EthAddress, ulong>();

            if (Database.GetBlockCount() == 0)
                await AddToBlockchain(Block.Genesis()).ConfigureAwait(false);
            else
            {
                blocks = Database.SelectAllBlocks();

                ulong i = 0;
                foreach (var blk in blocks)
                {
                    blockHashes.Add(blk.Hash, i++);
                    if (blk.Transactions != null)
                    {
                        foreach (var tx in blk.Transactions)
                        {
                            UpdateState(blk, i, tx);
                            transactions.Add(tx.Hash, tx);
                        }
                    }

                    ResetLastValidatedBlock(blk.Validator, i);
                }
            }
        }

        private static BlockchainHead GetHead()
        {
            if (blocks.Count == 0)
                return null;

            var index = blocks.Count - 1;
            var last = blocks[index];
            return new BlockchainHead { Index = index, Block = last };
        }

        private static int GetMinimumMajority(int groupCount)
        {
            if (groupCount <= 1)
                return 1;
            var b = groupCount / 2;
            var r = groupCount % 2;
            return b + r;
        }

        private static AccountState GetAccount(EthAddress address) =>
            accounts.ContainsKey(address) ? accounts[address] : null;

        public static AccountBalance GetBalanceOf(EthAddress address, HashKey32? exclude = null)
        {
            AccountState account;
            if (!accounts.TryGetValue(address, out account))
                return new AccountBalance();

            if (!exclude.HasValue)
                exclude = HashKey32.Empty;

            //resolve any transfers pending in the pool to adjust the available balance
            //ignore any incoming so the result is only a reduced balance. Akin to a bank balance with the current and available balances
            var userTransactions = txPool
                .Where(x => x.Value.From == address && x.Value.Type == Transaction_Type.Transfer && x.Key != exclude.Value)
                .OrderBy(x => x.Value.Nonce)
                .ToArray();

            //var balance = account.Balance;
            ulong pooledBalance = 0;

            foreach (var tx in userTransactions)
                pooledBalance += (tx.Value.Data.ToULong() + tx.Value.Fee);

            return new AccountBalance
            {
                Current = account.Balance,
                Available = account.Balance - pooledBalance
            };
        }

        private static ulong GetCurrentBalanceOf(EthAddress address)
        {
            AccountState account;
            if (!accounts.TryGetValue(address, out account))
                return 0;

            return account.Balance;
        }

        private static void SetBalanceOf(EthAddress address, ulong newAmount)
        {
            if (!accounts.ContainsKey(address))
                accounts.Add(address, new AccountState());

            accounts[address].Balance = newAmount;
        }

        public static bool IsValidator(EthAddress address, bool ignoreAge)
        {
            if (!registeredValidators.Contains(address))
                return false;

            ulong blockCount = (ulong)blocks.Count;

            if (blockCount <= Constants.MINIMUM_VALIDATOR_AGE)
                ignoreAge = true;

            if (ignoreAge)
                return true;

            if ((blockCount - validatorAges[address]) < Constants.MINIMUM_VALIDATOR_AGE)
                return false;

            return true;
        }

        private static int GetValidatorCount()
        {
            ulong blockCount = (ulong)blocks.Count;

            if (blockCount < Constants.MINIMUM_VALIDATOR_AGE)
                return registeredValidators.Count;

            return registeredValidators.Where(x => (blockCount - validatorAges[x]) >= Constants.MINIMUM_VALIDATOR_AGE).Count();
        }

        private static Dictionary<EthAddress, List<Transaction>> SortTransactionsByAddress()
        {
            var sortedByAddress = new SortedDictionary<EthAddress, List<Transaction>>();

            if (txPool.Count == 0)
                return new Dictionary<EthAddress, List<Transaction>>();

            foreach (var t in txPool.Values)
            {
                if (!sortedByAddress.ContainsKey(t.From))
                    sortedByAddress.Add(t.From, new List<Transaction>());

                sortedByAddress[t.From].Add(t);
            }

            return sortedByAddress.ToDictionary(x => x.Key, x => x.Value.OrderBy(x => x.Nonce).ToList());
        }

        private static uint GetTxNonce(EthAddress address)
        {
            AccountState account;
            if (!accounts.TryGetValue(address, out account))
                return 0;

            return account.Nonce;
        }

        private static uint GetNextTxNonce(EthAddress address)
        {
            uint current = 0;

            AccountState account;
            if (accounts.TryGetValue(address, out account))
                current = account.Nonce;

            var userTransactions = txPool
               .Where(x => x.Value.From == address)
               .OrderByDescending(x => x.Value.Nonce)
               .ToArray();

            uint expectedNonce = (userTransactions.Length >= 1 ? userTransactions[0].Value.Nonce : current) + 1;
            return expectedNonce;
        }

        private static void SetTxNonce(EthAddress address, uint newNonce)
        {
            if (!accounts.ContainsKey(address))
                accounts.Add(address, new AccountState());

            accounts[address].Nonce = newNonce;
        }

        private static void IncrementBalance(EthAddress address, ulong increment)
        {
            if (address == EthAddress.Empty)
                return;

            ulong balance = 0;

            if (accounts.TryGetValue(address, out AccountState account))
                balance = account.Balance;

            checked
            {
                var newBalance = balance + increment;
                SetBalanceOf(address, newBalance);
            }
        }

        private static void DecrementBalance(EthAddress address, ulong decrement)
        {
            if (address == EthAddress.Empty)
                return;

            ulong balance = 0;

            AccountState account;
            if (accounts.TryGetValue(address, out account))
                balance = account.Balance;

            checked
            {
                var newBalance = balance - decrement;
                SetBalanceOf(address, newBalance);
            }
        }

        private static bool EvaluateBalanceTransferState(List<Transaction> txs)
        {
            //accumulate total of balance transfers in this block so we can check that against the blockchain data
            var resolvedBalances = new Dictionary<EthAddress, long>();

            foreach (var tx in txs)
            {
                if (tx.Type != Transaction_Type.Transfer)
                    continue;

                if (!resolvedBalances.ContainsKey(tx.From))
                    resolvedBalances.Add(tx.From, 0);

                if (!resolvedBalances.ContainsKey(tx.To))
                    resolvedBalances.Add(tx.To, 0);

                resolvedBalances[tx.From] -= (long)tx.Data.ToULong();
                resolvedBalances[tx.To] += (long)tx.Data.ToULong();
            }

            foreach (var s in resolvedBalances)
            {
                var a = GetAccount(s.Key);
                long bal = a == null ? 0 : (long)a.Balance;
                if (bal + s.Value < 0)
                    return false;
            }

            return true;
        }

        private static bool EvaluateTransactionNonces(List<Transaction> txs)
        {
            var resolvedNonces = new Dictionary<EthAddress, uint>();
            foreach (var tx in txs)
            {
                if (!resolvedNonces.ContainsKey(tx.From))
                {
                    AccountState account;
                    if (accounts.TryGetValue(tx.From, out account))
                        resolvedNonces.Add(tx.From, account.Nonce);
                }

                resolvedNonces[tx.From]++;

                if (tx.Nonce != resolvedNonces[tx.From])
                    return false;
            }

            return true;
        }

        private static bool EvaluateTransactionHashes(List<Transaction> txs)
        {
            foreach (var tx in txs)
                if (GetTransaction(tx.Hash) != null)
                    return false;

            return true;
        }

        private static List<Transaction> GetSortedTransactions()
        {
            var ret = new List<Transaction>();

            var sortedTransactions = SortTransactionsByAddress();

            foreach (var t in sortedTransactions)
            {
                var acc = GetAccount(t.Key);
                var currentNonce = acc.Nonce;

                foreach (var tt in t.Value)
                {
                    if (transactions.ContainsKey(tt.Hash))
                    {
                        txPool.Remove(tt.Hash);
                        continue;
                    }

                    ++currentNonce;

                    // Transaction with non-sequential nonce. Ignore it and it will drop
                    // when the issuing node requests it or it times out
                    if (tt.Nonce != currentNonce)
                        break;

                    ret.Add(tt);
                }
            }

            return ret;
        }

        private static Block GetBlock(int index) => index >= blocks.Count ? null : blocks[index];

        private static List<Block> GetBlocks(int start, int count)
        {
            if (start >= blocks.Count)
                return new List<Block>();

            return blocks.Skip(start).Take(count).ToList();
        }

        private static Block GetBlock(HashKey32 hash) => blockHashes.ContainsKey(hash) ? blocks[(int)blockHashes[hash]] : null;

        private static Transaction GetTransaction(HashKey32 hash) => transactions.ContainsKey(hash) ? transactions[hash] : null;

        private static bool IsNextBlock(Block blk) => blk.LastHash == blocks[blocks.Count - 1].Hash;

        private static async Task<bool> AddToBlockPool(int index, Block blk)
        {
            try
            {
                if (blockPool.ContainsKey(index))
                    return false;

                if (blockHashes.ContainsKey(blk.Hash))
                    return false;

                if (index < (blocks.Count - 1))
                    return false;

                if ((index == 0 && blocks.Count == 0) || IsNextBlock(blk))
                    return await AddToBlockchain(blk).ConfigureAwait(false);

                blockPool.Add(index, blk);

                Log.Instance.WriteInfo($"Block {blk.Hash} added to pool");
                return true;
            }
            catch (Exception ex)
            {
                Log.Instance.WriteException(ex);
                Debugger.Break();
                return false;
            }
        }

        private static bool AddToTxPool(Transaction tx)
        {
            if (transactions.ContainsKey(tx.Hash))
                return false;

            if (txPool.ContainsKey(tx.Hash))
                return false;

            txPool.Add(tx.Hash, tx);

            return true;
        }

        private static bool AddToBidPool(NodeBid bid)
        {
            if (bid == null)
                return false;
                
            var lastHash = blocks[blocks.Count - 1].Hash;

            if (bid.LastBlockHash != lastHash)
                return false;

            var bidHash = NodeBid.GetHash(bid);

            if (bidPool.ContainsKey(bidHash))
                return false;

            if (!votePool.ContainsKey(bidHash))
                votePool.Add(bidHash, new Dictionary<HashKey32, NodeVote>());

            bidPool.Add(bidHash, bid);
            Log.Instance.WriteInfo($"Bid from {bid.Address} added to pool");

            return true;
        }

        private static bool AddToVotePool(NodeVote vote)
        {
            var lastHash = blocks[blocks.Count - 1].Hash;

            if (vote.LastBlockHash != lastHash)
                return false;

            var bidHash = NodeVote.GetBidHash(vote);
            var voteHash = NodeVote.GetHash(vote);

            if (!votePool.ContainsKey(bidHash))
                votePool.Add(bidHash, new Dictionary<HashKey32, NodeVote>());

            if (HasVoted(vote.LastBlockHash, vote.Address))
                return false;

            if (votePool[bidHash].ContainsKey(voteHash))
                return false;

            votePool[bidHash].Add(voteHash, vote);
            Log.Instance.WriteInfo($"{vote.Address} voted for {vote.VotedForAddress}");
            return true;
        }

        private static async Task<bool> AddToBlockchain(Block blk)
        {
            if (blocks.Count > 0)
            {
                var lastBlock = blocks[blocks.Count - 1];

                if (blk.LastHash != lastBlock.Hash)
                    return false; //this is not the next block to be added to the scene
            }

            Log.Instance.WriteInfo($"Block added to chain at index {blocks.Count}");

            blockHashes.Add(blk.Hash, (ulong)blocks.Count);
            blocks.Add(blk);

            //cache the transactions in the block
            foreach (var tx in blk.Transactions)
            {
                UpdateState(blk, (ulong)blocks.Count, tx);
                transactions.Add(tx.Hash, tx);
                txPool.Remove(tx.Hash);
            }

            ResetLastValidatedBlock(blk.Validator, (ulong)blocks.Count);
            BroadcastBid();

            await Database.InsertBlock(blk).ConfigureAwait(false);
            await Database.InsertBlockTransactions(blk).ConfigureAwait(false);
            Log.Instance.WriteInfo($"Adding block to chain {blk.Hash}");
            ApplicationLogWriter.WriteBuffered($"Adding block to chain {blk.Hash}{Environment.NewLine}", ConsoleColor.Cyan);

            return true;
        }

        private static void UpdateState(Block blk, ulong blkIndex, Transaction tx)
        {
            switch (tx.Type)
            {
                case Transaction_Type.Transfer:
                    ulong amount = tx.Data.ToULong();
                    DecrementBalance(tx.From, tx.Fee);
                    IncrementBalance(blk.Validator, tx.Fee);

                    DecrementBalance(tx.From, amount);
                    IncrementBalance(tx.To, amount);
                    break;
                case Transaction_Type.AddValidator:
                    if (!registeredValidators.Contains(tx.Data))
                    {
                        DecrementBalance(tx.From, tx.Fee);
                        IncrementBalance(blk.Validator, tx.Fee);

                        DecrementBalance(tx.From, Constants.VALIDATOR_STAKE);
                        registeredValidators.Add(tx.From);
                        validatorAges.Add(tx.From, blkIndex);
                        ResetLastValidatedBlock(tx.From, blkIndex);
                    }
                    break;
                case Transaction_Type.RemoveValidator:
                    if (registeredValidators.Contains(tx.Data))
                    {
                        DecrementBalance(tx.From, tx.Fee);
                        IncrementBalance(blk.Validator, tx.Fee);
                        
                        IncrementBalance(tx.From, Constants.VALIDATOR_STAKE);
                        registeredValidators.Remove(tx.From);
                        validatorAges.Remove(tx.From);
                        if (lastBlockHeights.ContainsKey(tx.From))
                            lastBlockHeights.Remove(tx.From);
                    }
                    break;
                default:
                    break;
            }

            SetTxNonce(tx.From, tx.Nonce);
        }

        private static void BroadcastBid()
        {
            var weight = CalculateNodeWeight(WalletStore.Current.Address);
            if (weight > 0)
            {
                var bid = NodeBid.Create(GetHead());
                if (!bid.Verify())
                {
                    Log.Instance.WriteError("Bid failed sanity check");
                    return;
                }

                if (!AddToBidPool(bid))
                    return;

#pragma warning disable CS4014
                MessageSender.BroadcastAsync(BidNetworkCommand.GenerateRequest(bid));
#pragma warning restore CS4014
            }
        }

        private static bool HasVoted(HashKey32 lastBlockHash, EthAddress nodeAddress)
        {
            foreach (var v in votePool)
                foreach (var vv in v.Value)
                    if (vv.Value.LastBlockHash == lastBlockHash && vv.Value.Address == nodeAddress)
                        return true;

            return false;
        }

        private static List<(NodeBid Bid, HashKey32 Hash, ulong Weight)> GetSortedBidList()
        {
            var requiredBidders = GetMinimumMajority(GetValidatorCount());

            if (bidPool.Count < requiredBidders)
            {
                Log.Instance.WriteInfo("Submit vote failed. Not enough bidders");
                return null;
            }

            var weights = new Dictionary<HashKey32, (NodeBid Bid, ulong Weight)>();
            foreach (var b in bidPool.Where(x => x.Value.LastBlockHash == blocks.Last().Hash))
                weights.Add(b.Key, (b.Value, CalculateNodeWeight(b.Value.Address)));

            if (weights.Count == 0)
                return null;

            var temp = new Dictionary<ulong, List<(HashKey32 Hash, NodeBid Bid)>>();

            foreach (var w in weights)
            {
                if (!temp.ContainsKey(w.Value.Weight))
                    temp.Add(w.Value.Weight, new List<(HashKey32, NodeBid)>());

                temp[w.Value.Weight].Add((w.Key, w.Value.Bid));
            }

            var sortedTemp = temp.OrderByDescending(x => x.Key).ToDictionary(x => x.Key, x => x.Value.OrderBy(x => x.Bid.Address));

            var ret = new List<(NodeBid Bid, HashKey32 Hash, ulong Weight)>();

            foreach (var sl in sortedTemp)
                foreach (var s in sl.Value)
                    ret.Add((s.Bid, s.Hash, sl.Key));

            return ret;
        }

        private static void BroadcastVote(EthAddress nodeAddress)
        {
            if (blocks == null || blocks.Count == 0)
                return;
                
            if (!isSynchronized)
                return;

            if (!IsValidator(nodeAddress, false))
                return;

            var sortedBids = GetSortedBidList();

            var address = sortedBids.ElementAt(0).Bid.Address;
            if (address == nodeAddress)
                return;

            var vote = NodeVote.Create(GetHead(), address);
            if (!vote.Verify())
            {
                Log.Instance.WriteError("Vote failed sanity check");
                return;
            }
            
            if (!AddToVotePool(vote))
                return;

#pragma warning disable CS4014
            MessageSender.BroadcastAsync(VoteNetworkCommand.GenerateRequest(vote).ToArray());
#pragma warning restore CS4014    
        }

        private static (bool Ok, NodeBid Bid, HashKey32 Hash, ulong Weight, List<NodeVote> Votes) GetConsensus()
        {
            var requiredParticipants = GetMinimumMajority(GetValidatorCount());

            var sortedBids = GetSortedBidList();

            if (sortedBids == null)
                return (false, null, HashKey32.Empty, 0, null);

            if (sortedBids.Count < requiredParticipants)
                return (false, null, HashKey32.Empty, 0, null);

            foreach (var b in sortedBids)
            {
                if (!votePool.ContainsKey(b.Hash))
                    continue;

                if (votePool[b.Hash].Count < requiredParticipants)
                    continue;

                return (true, b.Bid, b.Hash, b.Weight, votePool[b.Hash].Values.ToList());
            }

            return (false, null, HashKey32.Empty, 0, null);
        }

        private static ulong CalculateNodeWeight(EthAddress address)
        {
            //the weighting ensures you must have a coin balance and must be a registered validator to mint blocks
            if (!IsValidator(address, false))
                return 0;

            var factor = (ulong)(blocks.Count + 1) - (ulong)lastBlockHeights[address];
            var bal = GetBalanceOf(address).Available;

            try
            {
                checked
                {
                    return factor * bal;
                }
            }
            catch
            {
                return 1;
            }
        }

        private static void ResetLastValidatedBlock(EthAddress address, ulong height)
        {
            if (!lastBlockHeights.ContainsKey(address))
                lastBlockHeights.Add(address, height);

            lastBlockHeights[address] = height;
        }

        private static async void ProcessBlockPool()
        {
            var pruneList = new List<int>();

            foreach (var h in blockPool)
                if (blockHashes.ContainsKey(h.Value.Hash))
                    pruneList.Add(h.Key);

            foreach (var p in pruneList)
                blockPool.Remove(p);

            //sort the list and then attempt to add them to the chain one by one. when the first break in the chain is found, quit
            var sortedCopy = blockPool.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

            foreach (var s in sortedCopy)
            {
                if (IsNextBlock(s.Value))
                {
                    var noncesOk = EvaluateTransactionNonces(s.Value.Transactions);
                    if (!noncesOk)
                    {
                        Log.Instance.WriteError("Block contains an invalid transaction nonce. Dropping from pool.");
                        blockPool.Remove(s.Key);
                        break;
                    }

                    var balanceStateOk = EvaluateBalanceTransferState(s.Value.Transactions);
                    if (!balanceStateOk)
                    {
                        Log.Instance.WriteError("Invalid block. Applying the balance transfer state of the block will force one or more accounts into negative balance.");
                        blockPool.Remove(s.Key);
                        break;
                    }

                    var txHashesOk = EvaluateTransactionHashes(s.Value.Transactions);
                    if (!txHashesOk)
                    {
                        Log.Instance.WriteError("Invalid block. One or more transaction hashes already used.");
                        blockPool.Remove(s.Key);
                        break;
                    }

                    var addedToBlockchain = await AddToBlockchain(s.Value).ConfigureAwait(false);

                    if (!addedToBlockchain)
                        Log.Instance.WriteError("Adding block to chain failed");

                    blockPool.Remove(s.Key);
                }
                else break;
            }

            isSynchronized = blockPool.Count == 0;
        }

        private static void CleanTxPool()
        {
            foreach (var t in txPool.ToArray())
            {
                if (transactions.ContainsKey(t.Key))
                    txPool.Remove(t.Key);
            }
        }

        private static void CleanVotingPool()
        {
            if (!isSynchronized)
                return;

            var lastHash = blocks[blocks.Count - 1].Hash;

            foreach (var c in bidPool.ToArray())
            {
                if (c.Value.LastBlockHash == lastHash)
                    continue;

                if (blockHashes.ContainsKey(c.Value.LastBlockHash))
                    bidPool.Remove(c.Key);
            }

            foreach (var c in votePool.ToArray())
            {
                if (c.Value.Count == 0)
                    continue;

                var first = c.Value.ElementAt(0);

                if (first.Value.LastBlockHash == lastHash)
                    continue;

                if (blockHashes.ContainsKey(first.Value.LastBlockHash))
                    votePool.Remove(c.Key);
            }
        }

        private static void InitiateSync(int expectedResponses)
        {
            Log.Instance.WriteInfo("Initiating sync");
            var head = GetHead();
            if (head == null)
                syncState = new SyncState
                {
                    ExpectedResponses = expectedResponses,
                    LocalIndex = -1,
                    LocalHash = HashKey32.Empty
                };
            else
                syncState = new SyncState
                {
                    ExpectedResponses = expectedResponses,
                    LocalIndex = head.Index,
                    LocalHash = head.Block.Hash
                };
        }

        private static (int NewHeight, int RequiredBlocks) ProcessSyncResponse(int responseIndex, HashKey32 responseHash, Connection c, int threshold)
        {
            if (!syncState.Responses.ContainsKey(responseIndex))
                syncState.Responses.Add(responseIndex, new List<(HashKey32, Connection)>());

            syncState.Responses[responseIndex].Add((responseHash, c));

            foreach (var r in syncState.Responses)
                syncState.TotalResponses += r.Value == null ? 0 : r.Value.Count;

            var requiredResponses = GetMinimumMajority(syncState.ExpectedResponses);

            if (syncState.TotalResponses < requiredResponses)
            {
                Log.Instance.WriteInfo($"Insufficient responses {syncState.TotalResponses}/{requiredResponses}. Sync status unchanged");
                if (isSynchronized)
                    BroadcastBid();
                return (-1, -1);
            }

            //get responses with a higher height than us
            var higherNodes = syncState.Responses.Where(x => x.Key > syncState.LocalIndex).ToArray();
            if (higherNodes.Length == 0)
            {
                if (!isSynchronized)
                    Log.Instance.WriteInfo("In sync");

                BroadcastBid();
                isSynchronized = true;
                return (-1, -1);
            }

            //sort higher nodes by the count of responses
            var sorted = higherNodes.OrderByDescending(x => x.Value.Count).ToDictionary(x => x.Key, x => x.Value).ToArray();
            //todo: need to filter out chains that don't match the top block hash of the majority
            var bestMatch = sorted[0]; //of all the responses, this height has the most. 

            var newHeight = bestMatch.Key;
            var requiredBlocks = newHeight - syncState.LocalIndex;

            if (requiredBlocks <= threshold)
            {
                if (!isSynchronized)
                    Log.Instance.WriteInfo("In sync");

                BroadcastBid();
                isSynchronized = true;
                return (-1, -1);
            }

            if (isSynchronized)
                Log.Instance.WriteInfo("Out of sync");
            isSynchronized = false;
            return (newHeight, requiredBlocks);
        }

        private static async Task<bool?> HandleIncomingBlock(int index, Block blk)
        {
            if (!isSynchronized)
                return null;

            var b = GetBlock(index);

            if (b != null)
            {
                //we already have this block in out chain
                //but check if the hash matches just to make sure this node isn't trying to send bad blocks
                if (b.Hash != blk.Hash)
                {
                    var bl = blocks;
                    Log.Instance.WriteError($"Block received has different hash for index {index}");
                    return false;
                }

                return null;
            }

            if (!IsNextBlock(blk))
            {
                Log.Instance.WriteError("Synchronized chain received new block that is not the next block. Possible internal error");
                return false;
            }

            bool validBlock = await blk.Verify().ConfigureAwait(false);

            //invalid block. ignore it and disconnect
            if (!validBlock)
            {
                Log.Instance.WriteError("Invalid block");
                return false;
            }

            return await AddToBlockchain(blk).ConfigureAwait(false);
        }

        private static async Task<bool> HandleIncomingBlocks(int index, List<Block> blks)
        {
            for (int i = 0; i < blks.Count; i++)
            {
                var idx = index + i;
                var blk = blks[i];
                var b = GetBlock(idx);

                if (b != null)
                {
                    //we already have this block in out chain
                    //but check if the hash matches just to make sure this node isn't trying to send bad blocks
                    if (b.Hash != blk.Hash)
                    {
                        var bl = blocks;
                        Log.Instance.WriteError($"Block received has different hash for index {index}");
                        return false;
                    }

                    continue;
                }

                bool validBlock = idx == 0 ? true : await blk.Verify().ConfigureAwait(false);

                if (!validBlock)
                    return false;

                await AddToBlockPool(idx, blk).ConfigureAwait(false);
            }

            ProcessBlockPool();

            return true;
        }
    }
}