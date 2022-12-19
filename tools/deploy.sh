#!/bin/bash
dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root_dir="$(cd "$(dirname "${dir}")" && pwd)"
workspace_dir="$(cd "$(dirname "${root_dir}")" && pwd)"

source ${workspace_dir}/credentials
source ${dir}/version
runtime=net6.0

function _deploy_app() {
    HOST=$1
    echo deploying to $HOST
    echo "Uploading binary"
    scp -i ${SSH_KEY} -P ${PORT} \
        ${root_dir}/Node/Bin/${CONFIG}/${runtime}/linux-x64/publish/Node \
        ${USER}@${HOST}:~/Node
}

function _deploy_file() {
    HOST=$1
    SRC=$2
    DEST=$3
    echo Deploying file ${SRC} to ${HOST}:${DEST}
    scp -i ${SSH_KEY} -P ${PORT} ${dir}/${SRC} ${USER}@${HOST}:${DEST}
}

function app() {
    for i in "${servers[@]}"; do
        _deploy_app $i
    done
    exit
}

function script() {
    for i in "${servers[@]}"; do
        _deploy_file $i "scripts/node.sh" "~/node.sh"
    done

    _deploy_file ${servers[0]} "scripts/seednode.sh" "~/node.sh"
    exit
}

$1
