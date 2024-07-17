#!/bin/bash

# ---- Start of restore correct version of ezyBuild ----
export MSYS_NO_PATHCONV=1 # Disable Git Bash automatic path conversion. Otherwise, mounts inside container are prefixed with '\Program Files\Git'.
set -e # terminate when a command returns with non-0 exit code.
source ./build/.env # Read env variables from file, including EZY_BUILD_VERSION
if [ -z ${EZY_BUILD_VERSION} ]; then
  echo "Error: EZY_BUILD_VERSION is not set or is empty."
  exit 1
fi

ezyBuildDir="./packages/ezybuild/${EZY_BUILD_VERSION}"
if [ ! -d "$ezyBuildDir" ]; then
  echo "fetching ezyBuild version: ${EZY_BUILD_VERSION}"
  docker run \
    --rm \
    --name ezyBuild-ezy.dotnetsdk-restore-ezybuild-${EZY_BUILD_VERSION} \
    -e GCR_PAT=$GCR_PAT \
    -e EZY_BUILD_VERSION=$EZY_BUILD_VERSION \
    -v "$(pwd)":/app \
    ghcr.io/ezywebwerkstaden/ezy.dotnetcoresdk:3.1-latest \
    /bin/sh -c " \
      cd /app && \
      dotnet restore --packages ./packages /p:BaseIntermediateOutputPath='..\\.ezyBuild\\obj' ./build/restore-ezyBuild.csproj"
else
  echo "Skip fetching ezyBuild version: ${EZY_BUILD_VERSION}, package already downloaded"
fi
# ---- End of restore correct version of ezyBuild ----

# ---- Launch main agent startup file ----
source "${ezyBuildDir}/_dev/agent-main.sh"
