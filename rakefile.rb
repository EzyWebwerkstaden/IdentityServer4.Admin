require 'rexml/document'
include REXML
root = File.dirname(__FILE__)

# Section 1 - Bootstrapper: fetch ezyBuild-Rake from our nuget package in GitHub. GCR_PAT env var is required to be present.
packages_xml = Document.new(File.open('packages.config'))
ezyBuild_version = XPath.first(packages_xml, "/packages/package[@id='EzyWebwerkstaden.ezyBuild-Rake']").attributes.get_attribute("version").value
ezyBuildDir = "./packages/EzyWebwerkstaden.ezyBuild-Rake.#{ezyBuild_version}/build"
if (!File.exists?(ezyBuildDir))
  puts "fetching ezyBuild-Rake version: #{ezyBuild_version}"
  # Fetch with Mono image which contains nuget. Nuget is easier to work with non-dotnet packages than dotnet restore.
  # TODO: do GCR_PAT so that it is NOT printed to the output
  sh "docker run --rm -e GCR_PAT=#{ENV["GCR_PAT"]} -v #{root}:/app mono:6.12.0.107 /bin/sh -c \" \
      cd /app && \      
      nuget restore ./packages.config -PackagesDirectory ./packages\""
elsif
  puts "Skip fetching ezyBuild-Rake version: #{ezyBuild_version}, package already downloaded"
end

# Section 2 - Body:
# Identity Server already comes with "build" dir, so let's change it to "rake-build"
ENV["PROJECT_BUILD_DIR"] = "rake-build"
require "#{ezyBuildDir}/rakefile-body.rb"