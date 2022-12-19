# AssetChain

AssetChain (working title) is a blockchain specicially designed for storing and managing ownership of digital assets. One of the inherent weaknesses of NFTs on other blockchains ios the fact that the asset itself is not stored on chain. This can result in dead or corrupted links for users assets. AssetChain overcomes this by facilitating the storage of digital assets directly on chain with full auditable tracking of ownership.

## Introduction

This document will outline the functionality of a new general purpose Proof of Stake blockchain, AssetChain. The motivating factor in developing a new blockchain was to create a decentralized mechanism for validating token movemements between chains using the civiport token bridging system, however being general purpose in nature means AssetChain can be used for the validation and storage or any kind of data

## AssetChain structure

At it's source, AssetChain is similar to a proof of stake system, with individual transactions containing arbitrary data. The transaction type stored in the transaction determines how the data is interpreted and validated along with the fee amount incurred by the transaction. This system then allows for new transaction types to be added without adjustement to the transaction format.

Blocks are forged by validator nodes using a Proof of Stake mechanism that encourages fair distribution of block validations depending on wallet balance. Blocks are only forged when
when one or more pending transactions are present in the transaction pool and the forger selection criteria have been met. This ensures blocks are only minted when required, reducing the blockchain size and sync time compared to traditional proof of work chains that may contain many empty blocks.

### Transaction format

1. unsigned short Version
2. unsigned byte Transaction Type
3. unsigned int Nonce
4. byte[20] From
5. byte[20] To
6. byte[variable] Data
7. byte[8] Fee
8. byte[32] Hash
9. byte[65] Signature

The `Hash` is derived from the combination of 1-7 and the `Signature` is derived by signing the hash with the private key associated with the `From` address.

### Block format

1. unsigned short Version.
2. unsigned long Timestamp
3. byte[32] Previous Block Hash
4. byte[20] Validator Address
5. Transaction[variable] Transactions
6. byte[32] Hash
7. byte[65] Signature

As with the Transaction object, the `Hash` is derived from 1-5 and the `Signature` is derived by signing the `Hash` with the private key. Additionally, transactions in the block must be sorted in ascending order, first by the TX `From` address and then by the TX nonce. Transactions are therefore executed in order of their nonce. Transaction nonces must be sequential and skipping a nonce will result in further transactions from that particular account being excluded from future blocks.
Blocks can contain one or more transactions, however empty blocks are not permitted on the chain and will be rejected by nodes that receive them. 

Transactions and blocks are signed using an R, S, V signature, utilising a chain id similar to Ethereum. Chain ID is a single byte value in the range of 0 - 255.

### Transaction Types

As previously stated, transactions act as a wrapped to post arbitrary data to the blockchain. How that data is validated and processed is determined by the transaction `type` field. The `type` field is a single unsigned byte, allowing for a possible 255 different transaction types to be stored on the chain. 

### Chain State

The chain state represents the current state of every account on the chain. For each account the last used transaction nonce and account balance are stored in memory. The state is updated with each new block added to the chain.  
This allows fast look up of the current state of any account.

### Block consensus

When a new block is minted a new consensus round is initiated. When a block is added to the chain to the chain, all validators automatically broadcast a bid to be the next node to forge a block. When more than 50% of the registered validators have submitted a bid, all nodes on the network submit a vote for which block they deem to be the next forger. Voting is automated based on a weighting value of the bidding node. The weight is calculated based on the time from the last block and account balance.

The weighting is calculated as the current wallet balance * the time since last block. This promotes even distribution of block validations among nodes, proportionate to their wallet balance. For example, alice has a wallet balance of 100 tokens and bob has a balance of 30 tokens
Alices initial weighting is 100, while bob is 30. This means alice will be the validator.  
Now, since it has been 2 blocks since bob has validated, his weighting is now 60. Alice will validate the next block
After 3 blocks, bobs weight is 90 and after 4 blocks, Bob's weighting is 120 and will forge the next block, which will reset his weighting back to 30.

As we see, Bob's is validating every 4th block, which is proportial to his holding being 1/3 of Alice's

Each node in the system maintains the state of the current voting round and will only add a new block to the chain from the node this internal state calculates to be the next forger in the queue.

### Block pool and blockchain synchronization

The system uses the concept of a block pool to store blocks temporarily to be later sorted and added to the blockchain. This is used as Blockchain synchronization is highly parallelized and blocks being generated on demand can result in multiple blocks being forged in quick succession. Due to network latency and parallel sync requests, blocks may be received by a node out of order to what they are added to the chain.

The block pool is a temporary cache for received blocks. When a new block is received, it is added to this pool. When a new block is added to the pool, they are then sorted by height and added to the pool in the correct order. When a missing block is detected in the pool processing, a request is sent to a random node for the missing block. When the block is received, pool processing continues. This process is repeated until there are no blocks remaining in the pool.

To synchronize the chain a node must first determine which chain to sync to. An out of sync node will check it's connected peers and resolve to sync the chain that most of it's peers are on. This is determined to be the chain with the largest number of peers with the same height and top block hash. This is not necessarily the longest chain reported by a node. 

The synchronization worker will request the latest block from a random node in this group. When this block is received it is added to the block pool and the worker processes the block pool. The worker can determine the number of blocks that need to be downloaded to sync the chain. requests are sent to sync batches of 10 blocks with each request sent to a random node. 10 blocks was selected as a balance between parallelization and speed of response. 

when each group of blocks is received they are added to the block pool and the process is repeated. The sync is finished when all blocks in the pool have been processed, which is only possible if all the blocks between the current block and the top block requested at the start have all been downloaded. New blocks generated by the network during the sync process are simply added to the block pool and processed when all missing blocks are downloaded.

### Sending a transaction

Transactions can be sent in 2 ways. Either through the command line interface of the blockchain node, or via an RPC interface made available by the node on port 10001 (default). When a transaction is received via RPC it is first validated by the node. Any IP address sending invalid transactions to the node will be blocked. 

The receiving node then broadcasts the transaction to all connected peers. Each node that receives a transaction message will validate the transaction and drop the transaction and sending node if validation fails. When the pre-determined block forger receives the minimum number of transactions for a block, the node forges a new block and broadcasts the new block to the network.

Nodes periodically run a background worker to remove transactions from their transaction pool that have been included in a block. Nodes will also send requests to eachother to sync the transactions in the pool across the network.

### Block processing

As stated previously, nodes track the current state of every address used on the blockchain and update that state as they process blocks in the chain. The address state consists of the current balance of the node and the last transaction nonce used by the address. When a node receives a block it processes each transaction in the block in order, according to the type of transaction it is. For each transaction the nonce for the sender is updated to reflect the last used nonce. For token transfers, the balance is updated for the addresses in the `To` and `From` field, where the amount in the transaction is deducted from the `From` address and added to the `To` address. Each transaction deducts the fee amount from the `From` address and the sum of all fees in the block is added to the validators token balance as reward for running a node and forging the block.

This simplistic account based model gives an easy way to check balances for any address on the system and does away with the limitations of the UTXO model as locked change and a lack of transaction inputs. Additionally, transactions are always the same size (depending on the transaction type) and incur the same fee which can be known ahead of time. This provides cost certainty to users of the system.

### Blockchain data

Each transaction stores transaction specific data as a variable length byte array. The data is a consistent length for each type. As a result, the fee for any given transaction can be derived deterministically from the number of bytes and the type of transaction. The fee is calculated for each transac tion during block processing and the state of the validating address is updated to reflect the addition of the cumulative fees for the block transactions.

## Validation

Block validators forge and broadcast new blocks to AssetChain.

Any user can become a block validator by staking a fixed amount of tokens on AssetChain. To stake tokens on AssetChain a user submits a transaction to stake on AssetChain. Staking on AssetChain allows a node to become a block forger. There are currently 8 validator slots on AssetChain for block validators

## Fees and validator rewards

Each transaction incurs a fee. Fixed data length transactions have a fixed fee, while variable data length transactions have a base cost multiplied by the number of bytes in the transaction. This makes transactions that consume more blockchain space more expensive to execute. 

The block forger will get the fees for all the transactions in the block as a reward for their participation.
