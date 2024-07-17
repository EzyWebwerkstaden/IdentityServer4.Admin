# ---- Start of duplicated content with ezyDevOps/rakefile.rb. Make sure you modify both or extract this part ----
$container_engine = ENV["CONTAINER_ENGINE"] || "podman"
$inner_container_runas = (!ENV["RUN_INNER_CONTAINERS_AS_ROOT"].nil? && ENV["RUN_INNER_CONTAINERS_AS_ROOT"] == "true") ? "0:0" : "$(id -u):$(id -g)"
puts "CONTAINER_ENGINE: #{$container_engine}"
puts "RUN_INNER_CONTAINERS_AS_ROOT: #{ENV["RUN_INNER_CONTAINERS_AS_ROOT"]}"
# ---- End of duplicated content ----

# ---- Start of restore correct version of ezyBuild ----
def load_env_file(file)
  File.foreach(file) do |line|
    # Skip empty lines and lines that start with a comment (#)
    next if line.strip.empty? || line.strip.start_with?('#')

    key, value = line.strip.split('=', 2)
    # Remove surrounding quotes if any
    value = value.gsub(/\A"|"\Z/, '') if value

    # Set the environment variable
    ENV[key] = value if key && value
  end
end

def check_variable(var_name)
  var_value = ENV[var_name]
  if var_value.nil? || var_value.empty?
    puts "Error: #{var_name} is not set or is empty."
    exit 1
  else
    puts "#{var_name} is set to #{var_value}"
  end
end

load_env_file("./build/.env")
check_variable('EZY_BUILD_VERSION')
ezyBuild_version = ENV["EZY_BUILD_VERSION"]
ezyBuildDir = "./packages/ezybuild/#{ezyBuild_version}"
if (!File.exists?(ezyBuildDir))
  puts "fetching ezyBuild version: #{ezyBuild_version}"
  # not sure why, but when running "dotnet restore" from rakefile, unlike from agent.sh, I have to set the variable explicitly before executing "dotnet restore".
  # Otherwise, even if the env var is present inside the container, the "dotnet restore" doesn't 'see' it.
  sh "#{$container_engine} run \
        --rm \
        --name ezyBuild-ezy.dotnetsdk-restore-ezybuild-#{ezyBuild_version} \
        --user #{$inner_container_runas} \
        --env-file /opt/.ezyBuild/.env/github_ezy.env \
        -e EZY_BUILD_VERSION=$ezyBuild_version \
        -v #{Dir.pwd}:/app \
        ghcr.io/ezywebwerkstaden/ezy.dotnetcoresdk:3.1-latest \
        /bin/sh -c \" \
          cd /app && \
          EZY_BUILD_VERSION=$EZY_BUILD_VERSION dotnet restore --packages ./packages /p:BaseIntermediateOutputPath='..\\.ezyBuild\\obj' ./build/restore-ezyBuild.csproj\""
elsif
  puts "Skip fetching ezyBuild version: #{ezyBuild_version}, package already downloaded"
end
# ---- End of restore correct version of ezyBuild ----

# ---- Launch main rakefile ----
require "./packages/ezybuild/#{ezyBuild_version}/rakefile-main.rb"
