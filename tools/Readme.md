Node --rpc --wallet-file "<wallet-name>" --p2p-port 10000 --rpc-port 10001 --add-peer 167.86.96.126:10000

Omit --rpc to disable the rpc server for the node

RpcClient --wallet-file "<wallet-name>" --rpc 167.86.96.126:10001

--wallet-file can be omitted to get a default name. Only needed for using multiple wallets