#!/usr/bin/env bash
set -euo pipefail

IMAGE="mcr.microsoft.com/dotnet/sdk:9.0"
CONTAINER_NAME="ime-locker-build"
OUTPUT_DIR="publish"
CONFIGURATION="${1:-Release}"
RUNTIME="${2:-win-x64}"

echo "=== ImeLocker 容器化编译 ==="
echo "配置: ${CONFIGURATION}"
echo "目标运行时: ${RUNTIME}"
echo "容器引擎: podman"
echo ""

# 确保输出目录存在
mkdir -p "${OUTPUT_DIR}"

# 拉取镜像（如果不存在）
if ! podman image exists "${IMAGE}" 2>/dev/null; then
    echo "拉取 .NET SDK 镜像..."
    podman pull "${IMAGE}"
fi

echo "开始编译..."
podman run --rm \
    --name "${CONTAINER_NAME}" \
    -v "$(pwd):/src:Z" \
    -w /src \
    "${IMAGE}" \
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
