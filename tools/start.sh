#!/bin/bash
dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root_dir="$(cd "$(dirname "${dir}")" && pwd)"
workspace_dir="$(cd "$(dirname "${root_dir}")" && pwd)"

source ${workspace_dir}/credentials

function _work() {
    HOST=$1
    echo Working on Node $HOST
    ssh -i ${SSH_KEY} -p ${PORT} ${USER}@${HOST} 'screen -Smd node ./node.sh'
}

function work() {
    for i in "${servers[@]}"; do
        _work $i
    done
    exit
}

work
