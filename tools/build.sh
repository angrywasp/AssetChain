#!/bin/bash -e
dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root_dir="$(cd "$(dirname "${dir}")" && pwd)"
workspace_dir="$(cd "$(dirname "${root_dir}")" && pwd)"

source ${workspace_dir}/credentials
source ${dir}/version
runtime=net6.0

function buildPlatform() {
    software=$1
    platform=$2

    zip_name=AssetChain-$(echo "${software}" | tr '[:upper:]' '[:lower:]')-${VERSION}-${platform}-$(echo "${CONFIG}" | tr '[:upper:]' '[:lower:]').zip

    mkdir -p ${dir}/${VERSION}
    rm -f ${dir}/${VERSION}/${zip_name}

    rm -rf ${workspace_dir}/Blockchain/${software}/Bin
    rm -rf ${workspace_dir}/Blockchain/${software}/obj/${CONFIG}/${runtime}/${platform}
    rm -rf ${workspace_dir}/Blockchain/${software}/obj/${CONFIG}/${runtime}/${platform}/ref

    dotnet publish -c ${CONFIG} -f ${runtime} -r ${platform} --self-contained true \
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:PublishReadyToRunShowWarnings=true ${workspace_dir}/Blockchain/${software}/${software}.csproj

    rm -f ${workspace_dir}/Blockchain/${software}/Bin/${CONFIG}/${runtime}/${platform}/publish/*.pdb
    rm -f ${workspace_dir}/Blockchain/${software}/Bin/${CONFIG}/${runtime}/${platform}/publish/*.xml

    zip -j ${dir}/${VERSION}/${zip_name} ${workspace_dir}/Blockchain/${software}/Bin/${CONFIG}/${runtime}/${platform}/publish/*
}

function node() {
    #buildPlatform RpcClient osx-arm64
    #buildPlatform RpcClient osx-x64
    #buildPlatform RpcClient win-x64
    #buildPlatform RpcClient linux-x64

    #buildPlatform Node osx-arm64
    #buildPlatform Node osx-x64
    #buildPlatform Node win-x64
    #buildPlatform Node linux-x64

    exit
}

node
