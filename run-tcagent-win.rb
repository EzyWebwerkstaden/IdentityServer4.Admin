# * Allows testing all rake commands inside teamcity build agent docker image, so in exactly same environment as Remotely.
# * Since ezyBuild-1.0.153 version, execution of rake commands directly on windows IS NO LONGER SUPPORTED!
# * DO NOT connect to current teamcity server using this script (leave the SERVER_URL as is)#
#   To do that, use the src\projects\ezyTeamCityAgent\run-tc-agent-local.bat script
# * Environment Variables: All env vars are mounted in the /opt/.ezyBuild/env folder, to be later mounted further in eventual worker-containers with --env-file param.
#     This is to not need to keep secrest in ENV vars in the main tc agent image (they are all visible .e.g in the TC UI page for the agent)
# * Docker registries config: mounted to /home/buildagent/.docker/config.json allows access to registries (GCR & Nexus).
# * RUN_INNER_CONTAINERS_AS_ROOT=false. When mounting our sources into /app from Windows, passing the $(id -u):$(id -g) to inner containers work well - i.e.
#   they don't complain about not enough permissions to create dirs or files.
# * Plugging directories for TC-related diagnosis
#   -v #{project_dot_ezy_build_dir}/.tc/ezy-tc-agent-local-#{current_dir_name}/conf:/data/teamcity_agent/conf \
#   -v #{project_dot_ezy_build_dir}/.tc/ezy-tc-agent-local-#{current_dir_name}/work":/opt/buildagent/work \
#   -v #{project_dot_ezy_build_dir}/.tc/ezy-tc-agent-local-#{current_dir_name}/temp":/opt/buildagent/temp \
#   -v #{project_dot_ezy_build_dir}/.tc/ezy-tc-agent-local-#{current_dir_name}/tools":/opt/buildagent/tools \
#   -v #{project_dot_ezy_build_dir}/.tc/ezy-tc-agent-local-#{current_dir_name}/plugins":/opt/buildagent/plugins \
#   -v #{project_dot_ezy_build_dir}/.tc/ezy-tc-agent-local-#{current_dir_name}/system":/opt/buildagent/system \
#
# * Where to get secrets from?:
#   * GCR_USERNAME: ezydeploy"
#   * GCR_PAT: 1Password: 'ezydeploy account on github.com' / 'ContainerRegistry PAT'
#   * NUGET_SABRE_NEXUS_STAGING_API_KEY: 1Password: 'svc-ark-radixxezy' / 'NuGet API Key'
#   * NEXUS_USER: 1Password: 'svc-ark-radixxezy' / 'user token name'
#   * NEXUS_PASSWORD: 1Password: 'svc-ark-radixxezy' / 'pass code'
#   * SCAN_USER: 1Password: 'svc-ark-radixxezy' / 'username'
#   * SCAN_PASSWORD: 1Password: 'svc-ark-radixxezy' / 'promotion jenkins token (cicd)'
#   * OCTOPUS_EZY_ONPREM_APIKEY: 1Password: 'Octopus Api Key (for GCP)' / 'password'
#
# * AWS secrets (only when needing to run tentancle from under tcagent. It's useful, when you build new image(s) and you don't want to wait until it's pushed to github and then downloaded directly to your machine)
#   Obtain session token by "aws sts get-session-token --serial-number arn-of-your-mfa-device --token-code mfa-token"
#   * AWS_DEFAULT_REGION="us-east-1"
#   * AWS_ACCESS_KEY_ID=""
#   * AWS_SECRET_ACCESS_KEY=""
#   * AWS_SESSION_TOKEN="" - 

require "base64"
execution_path = Dir.pwd
current_dir_name = execution_path.split('/').last
puts "execution_path: #{execution_path}"
puts "current_dir_name: #{current_dir_name}"
project_dot_ezy_build_dir = "#{execution_path}/.ezyBuild"
home_dot_ezy_build_dir = "#{Dir.home}/.ezyBuild"
home_dot_ezy_deploy_dir = "#{Dir.home}/.ezyDeploy"

Dir.mkdir(project_dot_ezy_build_dir) unless File.exists?(project_dot_ezy_build_dir)
Dir.mkdir(home_dot_ezy_build_dir) unless File.exists?(home_dot_ezy_build_dir)
Dir.mkdir(home_dot_ezy_deploy_dir) unless File.exists?(home_dot_ezy_deploy_dir)

# Write .env for ezyBuild.
home_dot_ezy_build_env_dir = "#{home_dot_ezy_build_dir}/.env"
Dir.mkdir(home_dot_ezy_build_env_dir) unless File.exists?(home_dot_ezy_build_env_dir)
github_env_file = "#{home_dot_ezy_build_env_dir}/github_ezy.env"
nexus_env_file = "#{home_dot_ezy_build_env_dir}/sabre_nexus.env"
scan_env_file = "#{home_dot_ezy_build_env_dir}/sabre_scan.env"
octopus_ezy_onprem_env_file = "#{home_dot_ezy_build_env_dir}/octopus_ezy_onprem.env"
octopus_sabre_gke_env_file = "#{home_dot_ezy_build_env_dir}/octopus_sabre_gke.env"
File.write(github_env_file, "GCR_USERNAME=#{ENV["GCR_USERNAME"]}\nGCR_PAT=#{ENV["GCR_PAT"]}") unless File.exists?(github_env_file)
File.write(nexus_env_file, "NUGET_SABRE_NEXUS_STAGING_API_KEY=#{ENV["NUGET_SABRE_NEXUS_STAGING_API_KEY"]}\nNEXUS_USER=#{ENV["NEXUS_USER"]}\nNEXUS_PASSWORD=#{ENV["NEXUS_PASSWORD"]}") unless File.exists?(nexus_env_file)
File.write(scan_env_file, "SCAN_USER=#{ENV["SCAN_USER"]}\nSCAN_PASSWORD=#{ENV["SCAN_PASSWORD"]}") unless File.exists?(scan_env_file)
File.write(octopus_ezy_onprem_env_file, "OCTOPUS_APIKEY=#{ENV["OCTOPUS_EZY_ONPREM_APIKEY"]}") unless File.exists?(octopus_ezy_onprem_env_file)
File.write(octopus_sabre_gke_env_file, "OCTOPUS_APIKEY=#{ENV["OCTOPUS_SABRE_GKE_APIKEY"]}") unless File.exists?(octopus_sabre_gke_env_file)

# Write .env files for ezyDeploy
home_dot_ezy_deploy_env_dir = "#{home_dot_ezy_deploy_dir}/.env"
Dir.mkdir(home_dot_ezy_deploy_env_dir) unless File.exists?(home_dot_ezy_deploy_env_dir)
aws_ezyradixx_env_file = "#{home_dot_ezy_deploy_env_dir}/aws_ezyradixx.env"
File.write(aws_ezyradixx_env_file, "AWS_DEFAULT_REGION=#{ENV["AWS_DEFAULT_REGION"]}\nAWS_ACCESS_KEY_ID=#{ENV["AWS_ACCESS_KEY_ID"]}\nAWS_SECRET_ACCESS_KEY=#{ENV["AWS_SECRET_ACCESS_KEY"]}\nAWS_SESSION_TOKEN=#{ENV["AWS_SESSION_TOKEN"]}") unless File.exists?(aws_ezyradixx_env_file)

# Assume TF files that will be mounted in tc agent container. 
dot_tf_dir = "#{home_dot_ezy_build_dir}/.tf"
Dir.mkdir(dot_tf_dir) unless File.exists?(dot_tf_dir)
terraformrc_tf_file = "#{dot_tf_dir}/.terraformrc"
File.write(terraformrc_tf_file, "credentials \"tfe.prod.sabre-gcp.com\" { token = \"#{ENV["TF_TFE_PROD_TOKEN"]}\" }") unless File.exists?(terraformrc_tf_file)

# Write docker's config file that will be mounted in tc agent container. 
dot_docker_dir = "#{home_dot_ezy_build_dir}/.docker"
Dir.mkdir(dot_docker_dir) unless File.exists?(dot_docker_dir)
docker_config_file = "#{dot_docker_dir}/config.json"
if (!File.exists?(docker_config_file))
    # base64(username:password) produces the necessary token:
    github_ezy_token = Base64.strict_encode64("#{ENV["GCR_USERNAME"]}:#{ENV["GCR_PAT"]}")
    sabre_nexus_staging_token = Base64.strict_encode64("#{ENV["NEXUS_USER"]}:#{ENV["NEXUS_PASSWORD"]}")
    File.write(docker_config_file, "{\"auths\": {\"ghcr.io\": {\"auth\": \"#{github_ezy_token}\"},\"repository.sabre-gcp.com:9082\": {\"auth\": \"#{sabre_nexus_staging_token}\"}}}\n")
end

$container_engine = ENV["CONTAINER_ENGINE"] || "docker"
puts "CONTAINER_ENGINE: #{$container_engine}"
tcagent_extra_env_vars = ($container_engine == "docker") ? "-e DOCKER_IN_DOCKER=start" : ""
tcagent_image = ($container_engine == "docker") ? "ezy.teamcity.agent:2023.11.4-linux-sudo-4" : "ezy.teamcity.agent:2023.05.4-linux-1"
# Podman: As a non-root container user, container images are stored under your home directory ($HOME/.local/share/containers/storage/), instead of /var/lib/containers.
# https://access.redhat.com/documentation/en-us/red_hat_enterprise_linux_atomic_host/7/html/managing_containers/finding_running_and_building_containers_with_podman_skopeo_and_buildah
# Unfortunately, mounting this drive on Windwos (nfs fs) makes the directory owned by root, so not usable inside. The workaround would be to create 
# the volume on host system with buildagent as owner, however this would work only under linux.
tcagent_mount_container_engine_dir = ($container_engine == "docker") ? "-v #{$container_engine}_volumes_#{current_dir_name}:/var/lib/docker" : ""

task :default do
  sh "docker run -d \
        --pull always \
        --privileged \
        --name ezy-tc-agent-local-#{current_dir_name}-#{$container_engine} \
        #{tcagent_extra_env_vars} \
        -e SERVER_URL=https://do.not.connect \
        -e AGENT_NAME=ezy-tc-agent-local-#{current_dir_name}-#{$container_engine} \
        -e CONTAINER_ENGINE=#{$container_engine} \
        -e RUN_INNER_CONTAINERS_AS_ROOT=false \
        #{tcagent_mount_container_engine_dir} \
        -v #{dot_docker_dir}:/home/buildagent/.docker:ro \
        -v #{home_dot_ezy_build_env_dir}:/opt/.ezyBuild/.env:ro \
        -v #{dot_tf_dir}:/opt/.ezyBuild/.tf:ro \
        -v #{home_dot_ezy_deploy_env_dir}:/home/buildagent/.ezyDeploy/.env:ro \
        -v ezy.gcpcli.gcloud-config:/home/buildagent/.config/gcloud \
        -v #{execution_path}:/app \
      ghcr.io/ezywebwerkstaden/#{tcagent_image} > #{project_dot_ezy_build_dir}/.tcagent-local-container-id" 

  # Read container from file and exec into container
  container_id = text = File.read("#{project_dot_ezy_build_dir}/.tcagent-local-container-id").strip()  
  begin
    sh "docker exec -it -w /app #{container_id} /bin/bash"
  ensure    
    # To automatically remove tcagent container, uncomment the following section. However, with rootless/podman version we can't mount podman volumes externally 
    # on Windows, so we lose them once container exits. That's why, it's better to keep it for later re-attachment.
    # # on exit, clean up by removing the detached container
    # puts "removing container #{container_id}"
    # sh "docker rm -f #{container_id}"
  end
end