require 'rexml/document'
include REXML
project_root = Dir.pwd

# ---- Start of duplicated content with ezyDevOps/rakefile.rb. Make sure you modify both or extract this part --------
$container_engine = ENV["CONTAINER_ENGINE"] || "podman"
$inner_container_runas = (!ENV["RUN_INNER_CONTAINERS_AS_ROOT"].nil? && ENV["RUN_INNER_CONTAINERS_AS_ROOT"] == "true") ? "0:0" : "$(id -u):$(id -g)"
puts "CONTAINER_ENGINE: #{$container_engine}"
puts "inner_container_runas: #{$inner_container_runas}"
# ---- End of duplicated content --------

# Bootstrapper: fetch ezyBuild from our nuget package in GitHub. GCR_PAT env var is required to be present (in github_ezy.env file)
ezyBuild_project_xml = Document.new(File.open('build/restore-ezyBuild.csproj'))
ezyBuild_version = XPath.first(ezyBuild_project_xml, "/Project/ItemGroup/PackageReference[@Include='ezyBuild']").attributes.get_attribute("Version").value
ezyBuildDir = "./packages/ezybuild/#{ezyBuild_version}"
if (!File.exists?(ezyBuildDir))
  puts "fetching ezyBuild version: #{ezyBuild_version}"
  sh "#{$container_engine} run \
        --rm \
        --user #{$inner_container_runas} \
        --env-file /opt/.ezyBuild/.env/github_ezy.env \
        -v #{project_root}:/app \
        ghcr.io/ezywebwerkstaden/ezy.dotnetcoresdk:1.0.20--3.1.200-buster \
        /bin/sh -c \" \
          cd /app && \
          dotnet restore --packages ./packages /p:BaseIntermediateOutputPath='..\\.ezyBuild\\obj' ./build/restore-ezyBuild.csproj\"" 
elsif
  puts "Skip fetching ezyBuild version: #{ezyBuild_version}, package already downloaded"
end

# Body:
# If project can't place it's ProjectGroup / ToolsGroup files in "build" dir then override with the following:
#   ENV["PROJECT_BUILD_DIR"] = "different_folder/relative/path"
require "#{ezyBuildDir}/rakefile-body.rb"
