require 'rexml/document'
include REXML
root = File.dirname(__FILE__)

# Section 1 - Create .env file with all env variables that we don't want to be shown in the console.
puts "creating .env file."
File.open(".env", "w") do |f|
  f.write("GCR_PAT=#{ENV["GCR_PAT"]}")
end

# Section 2 - Bootstrapper: fetch ezyBuild-Rake from our nuget package in GitHub. GCR_PAT env var is required to be present (in .env file).
packages_xml = Document.new(File.open('packages.config'))
ezyBuild_version = XPath.first(packages_xml, "/packages/package[@id='EzyWebwerkstaden.ezyBuild-Rake']").attributes.get_attribute("version").value
ezyBuildDir = "./packages/EzyWebwerkstaden.ezyBuild-Rake.#{ezyBuild_version}/build"
if (!File.exists?(ezyBuildDir))
  puts "fetching ezyBuild-Rake version: #{ezyBuild_version}"
  sh "docker run --rm --env-file ./.env -v #{root}:/app ghcr.io/ezywebwerkstaden/ezy.devopstools /bin/sh -c \" \
      cd /app && \
      nuget restore ./packages.config -PackagesDirectory ./packages\""
elsif
  puts "Skip fetching ezyBuild-Rake version: #{ezyBuild_version}, package already downloaded"
end

# Section 2 - Body:
# Identity Server already comes with "build" dir, so let's change it to "rake-build"
ENV["PROJECT_BUILD_DIR"] = "rake-build"
require "#{ezyBuildDir}/rakefile-body.rb"