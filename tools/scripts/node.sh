#!/bin/bash
dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd )"

while true
do
        ${dir}/Node --rpc --wallet-file "node" --p2p-port 10000 --rpc-port 10001 --add-peer 167.86.96.126:10000 --password ""
        sleep 1
done
