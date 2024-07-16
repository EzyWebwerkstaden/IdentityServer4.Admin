#!/bin/bash

export MSYS_NO_PATHCONV=1 # Disable Git Bash automatic path conversion. Otherwise, mounts inside container are prefixed with '\Program Files\Git'.
set -e # terminate when a command returns with non-0 exit code.
# set -x  # Enable command printing (uncomment for debugging)

# * Environment Variables: All env vars are mounted in the /opt/.ezyBuild/env folder, to be later mounted further in eventual worker-containers with --env-file param.
#     This is to not need to keep secrest in ENV vars in the main tc agent image (they are all visible .e.g in the TC UI page for the agent)
# * RUN_INNER_CONTAINERS_AS_ROOT=false. When mounting our sources into /app from Windows, passing the $(id -u):$(id -g) to inner containers work well - i.e.
#   they don't complain about not enough permissions to create dirs or files.
# * To preserve the history, we instruct it to be saved into mounted folder by HISTFILE env var, but also have to add "-e HISTCONTROL=histappend".
#   This is because we firstly detach from session (in background) and attach to it via exec (so that we can attach to it later on).
#   Also -it options has to be present, even in the first container start to allow the history to be saved to file.
#
# * Where to get secrets from?:
#   * GCR_USERNAME: <your_github_username>
#   * GCR_PAT: <your_PAT_value> / https://github.com/settings/tokens
#   * NUGET_SABRE_NEXUS_STAGING_API_KEY: <your_Nexus_NuGet_api_key> / https://repository.sabre-gcp.com/#user/nugetapitoken
#   * NEXUS_USER: 1Password: 'your_Nexus_user_token_name> / https://repository.sabre-gcp.com/#user/usertoken
#   * NEXUS_PASSWORD: 1Password: 'your_Nexus_user_token_value> / https://repository.sabre-gcp.com/#user/usertoken
#   * SCAN_USER: <your-SG-number>
#   * SCAN_PASSWORD: <your_PromotionJenkins_token_value> / https://promote.repository.sabre-gcp.com/user/<your-sg-number>/configure 
#   * OCTOPUS_SABRE_GKE_APIKEY: <your_Octopus_api_key> / https://ezy-octopus-server-radixx-ezyc-dev-ops.apps.dev-01.us-central1.dev.sabre-gcp.com/app#/Spaces-1/users/me/apiKeys
#
# * AWS secrets (only when needing to run tentancle from under tcagent. It's useful, when you build new image(s) and you don't want to wait until it's pushed to github and then downloaded directly to your machine)
#   Obtain session token by "aws sts get-session-token --serial-number arn-of-your-mfa-device --token-code mfa-token"
#   * AWS_DEFAULT_REGION="us-east-1"
#   * AWS_ACCESS_KEY_ID=""
#   * AWS_SECRET_ACCESS_KEY=""
#   * AWS_SESSION_TOKEN="" -

# Initialize variables
execution_path=$(pwd)
current_dir_name=$(basename "$execution_path")
echo "execution_path: $execution_path"
echo "current_dir_name: $current_dir_name"

project_dot_ezy_build_dir="$execution_path/.ezyBuild"
home_dot_ezy_build_dir="$HOME/.ezyBuild"
home_dot_ezy_deploy_dir="$HOME/.ezyDeploy"

# Create necessary directories if they don't exist
mkdir -p "$project_dot_ezy_build_dir"
mkdir -p "$home_dot_ezy_build_dir"
mkdir -p "$home_dot_ezy_deploy_dir"

# Write .env for ezyBuild
home_dot_ezy_build_env_dir="$home_dot_ezy_build_dir/.env"
mkdir -p "$home_dot_ezy_build_env_dir"
github_env_file="$home_dot_ezy_build_env_dir/github_ezy.env"
nexus_env_file="$home_dot_ezy_build_env_dir/sabre_nexus.env"
scan_env_file="$home_dot_ezy_build_env_dir/sabre_scan.env"
octopus_sabre_gke_env_file="$home_dot_ezy_build_env_dir/octopus_sabre_gke.env"
[ ! -f "$github_env_file" ] && echo -e "GCR_USERNAME=${GCR_USERNAME}\nGCR_PAT=${GCR_PAT}" > "$github_env_file"
[ ! -f "$nexus_env_file" ] && echo -e "NUGET_SABRE_NEXUS_STAGING_API_KEY=${NUGET_SABRE_NEXUS_STAGING_API_KEY}\nNEXUS_USER=${NEXUS_USER}\nNEXUS_PASSWORD=${NEXUS_PASSWORD}" > "$nexus_env_file"
[ ! -f "$scan_env_file" ] && echo -e "SCAN_USER=${SCAN_USER}\nSCAN_PASSWORD=${SCAN_PASSWORD}" > "$scan_env_file"
[ ! -f "$octopus_sabre_gke_env_file" ] && echo -e "OCTOPUS_APIKEY=${OCTOPUS_SABRE_GKE_APIKEY}" > "$octopus_sabre_gke_env_file"

# Write .env files for ezyDeploy
home_dot_ezy_deploy_env_dir="$home_dot_ezy_deploy_dir/.env"
mkdir -p "$home_dot_ezy_deploy_env_dir"
aws_env_file="$home_dot_ezy_deploy_env_dir/aws.env"
[ ! -f "$aws_env_file" ] && echo -e "AWS_DEFAULT_REGION=${AWS_DEFAULT_REGION}\nAWS_ACCESS_KEY_ID=${AWS_ACCESS_KEY_ID}\nAWS_SECRET_ACCESS_KEY=${AWS_SECRET_ACCESS_KEY}\nAWS_SESSION_TOKEN=${AWS_SESSION_TOKEN}" > "$aws_env_file"

# Assume TF files that will be mounted in tc agent container
dot_tf_dir="$home_dot_ezy_build_dir/.tf"
mkdir -p "$dot_tf_dir"
terraformrc_tf_file="$dot_tf_dir/.terraformrc"
[ ! -f "$terraformrc_tf_file" ] && echo "credentials \"tfe.prod.sabre-gcp.com\" { token = \"${TF_TFE_PROD_TOKEN}\" }" > "$terraformrc_tf_file"

# Write docker's config file that will be mounted in tc agent container
dot_docker_dir="$home_dot_ezy_build_dir/.docker"
mkdir -p "$dot_docker_dir"
docker_config_file="$dot_docker_dir/config.json"
if [ ! -f "$docker_config_file" ]; then
    github_ezy_token=$(echo -n "${GCR_USERNAME}:${GCR_PAT}" | base64)
    sabre_nexus_staging_token=$(echo -n "${NEXUS_USER}:${NEXUS_PASSWORD}" | base64)
    echo "{\"auths\": {\"ghcr.io\": {\"auth\": \"${github_ezy_token}\"},\"repository.sabre-gcp.com:9082\": {\"auth\": \"${sabre_nexus_staging_token}\"},\"repository.sabre-gcp.com:9083\": {\"auth\": \"${sabre_nexus_staging_token}\"},\"repository.sabre-gcp.com:9085\": {\"auth\": \"${sabre_nexus_staging_token}\"}}}" > "$docker_config_file"
fi

CONTAINER_ENGINE="${CONTAINER_ENGINE:-docker}"
echo "CONTAINER_ENGINE: $CONTAINER_ENGINE"
container_name="ezy-tc-agent-local-${current_dir_name}-${CONTAINER_ENGINE}"
tcagent_extra_env_vars=""
tcagent_image="ezy.teamcity.agent"
# * Podman: As a non-root container user, container images are stored under your home directory ($HOME/.local/share/containers/storage/), instead of /var/lib/containers.
#   https://access.redhat.com/documentation/en-us/red_hat_enterprise_linux_atomic_host/7/html/managing_containers/finding_running_and_building_containers_with_podman_skopeo_and_buildah
#   Unfortunately, mounting this drive on Windwos (nfs fs) makes the directory owned by root, so not usable inside. The workaround would be to create
#   the volume on host system with buildagent as owner, however this would work only under linux.
# * Docker: can't share /var/lib/docker between multiple containers (at least when using hyper-v)
tcagent_mount_container_engine_dir=""

if [ "$CONTAINER_ENGINE" == "docker" ]; then
    tcagent_extra_env_vars="-e DOCKER_IN_DOCKER=start"
    tcagent_image="ezy.teamcity.agent"
    tcagent_mount_container_engine_dir="-v ${CONTAINER_ENGINE}_volumes_${current_dir_name}:/var/lib/docker"
else
    tcagent_image="ezy.teamcity.agent:2023.05.4-linux-1"
fi

container_exists() {
    docker ps -a --filter "name=$1" --format "{{.Names}}" | grep -w "$1" > /dev/null 2>&1
}

container_running() {
    docker inspect --format="{{.State.Running}}" "$1" 2>/dev/null | grep -w "true" > /dev/null 2>&1
}

if container_exists "$container_name"; then
    if container_running "$container_name"; then
        echo "Container $container_name is already created and started, just need to attach"
    else
        echo "Container $container_name is already created but is not in Running state, need to start it"
        docker start "$container_name"
    fi
else
    echo "Container $container_name does not exist, need to create it"
    docker run -it -d \
      --pull=always \
      --privileged \
      --name "$container_name" \
      $tcagent_extra_env_vars \
      -e START_AGENT=no \
      -e CONTAINER_ENGINE="$CONTAINER_ENGINE" \
      -e RUN_INNER_CONTAINERS_AS_ROOT=false \
      -e HISTFILE=/app/.ezyBuild/.bash_history \
      -e HISTCONTROL=histappend \
      $tcagent_mount_container_engine_dir \
      -v "$dot_docker_dir":/home/buildagent/.docker:ro \
      -v "$home_dot_ezy_build_env_dir":/opt/.ezyBuild/.env:ro \
      -v "$dot_tf_dir":/opt/.ezyBuild/.tf:ro \
      -v "$home_dot_ezy_deploy_env_dir":/home/buildagent/.ezyDeploy/.env:ro \
      -v ezy.gcpcli.gcloud-config:/home/buildagent/.config/gcloud \
      -v "$execution_path":/app \
      "ghcr.io/ezywebwerkstaden/$tcagent_image"
fi

echo "Attaching to: $container_name"
docker exec -it -w /app "$container_name" /bin/bash