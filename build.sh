#!/usr/bin/env bash
set -euo pipefail

DOTNET_IMAGE="mcr.microsoft.com/dotnet/sdk:9.0"
INNO_IMAGE="docker.io/amake/innosetup"
CONTAINER_NAME="ime-locker-build"
OUTPUT_DIR="publish"
INSTALLER_OUTPUT="output"
CONFIGURATION="${1:-Release}"
RUNTIME="${2:-win-x64}"

echo "=== ImeLocker 容器化编译 ==="
echo "配置: ${CONFIGURATION}"
echo "目标运行时: ${RUNTIME}"
echo "容器引擎: podman"
echo ""

# 确保输出目录存在
mkdir -p "${OUTPUT_DIR}" "${INSTALLER_OUTPUT}"

# 拉取镜像（如果不存在）
if ! podman image exists "${DOTNET_IMAGE}" 2>/dev/null; then
    echo "拉取 .NET SDK 镜像..."
    podman pull "${DOTNET_IMAGE}"
fi

echo "开始编译..."
podman run --rm \
    --name "${CONTAINER_NAME}" \
    -v "$(pwd):/src:Z" \
    -w /src \
    "${DOTNET_IMAGE}" \
    bash -c "
        dotnet restore
        dotnet publish src/ImeLocker/ImeLocker.csproj \
            -c ${CONFIGURATION} \
            -r ${RUNTIME} \
            --self-contained true \
            -p:PublishSingleFile=true \
            -p:PublishTrimmed=false \
            -o /src/${OUTPUT_DIR}
    "

echo ""
echo "=== 编译完成 ==="
echo "输出目录: ${OUTPUT_DIR}/"
ls -lh "${OUTPUT_DIR}/"

# 打包安装程序
echo ""
echo "=== 打包安装程序 ==="

if ! podman image exists "${INNO_IMAGE}" 2>/dev/null; then
    echo "拉取 Inno Setup 镜像..."
    podman pull "${INNO_IMAGE}"
fi

podman run --rm \
    --userns=keep-id \
    -v "$(pwd):/work:Z" \
    "${INNO_IMAGE}" \
    /work/installer/ImeLocker.iss

echo ""
echo "=== 安装包生成完成 ==="
echo "输出目录: ${INSTALLER_OUTPUT}/"
ls -lh "${INSTALLER_OUTPUT}/"
