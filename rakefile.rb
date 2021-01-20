
require 'rexml/document'
include REXML
root = File.dirname(__FILE__)

packages_xml = Document.new(File.open('packages.config'))
ezyBuild_version = XPath.first(packages_xml, "/packages/package[@id='EzyWebwerkstaden.ezyBuild-Rake']").attributes.get_attribute("version").value
puts "fetching ezyBuild-Rake version: #{ezyBuild_version}" 
# Fetch with Mono image which contains nuget. Nuget is easier to work with non-dotnet packages than dotnet restore.
sh "docker run --rm -v #{root}:/app mono:6.12.0.107 /bin/sh -c \" \
    cd /app && \
    nuget sources add -name 'github' -source 'https://nuget.pkg.github.com/ezywebwerkstaden/index.json' -username 'ezydeploy' -password #{ENV["GCR_PAT"]} && \
    nuget restore ./packages.config -PackagesDirectory ./packages\""

ezyBuildDir = "./packages/EzyWebwerkstaden.ezyBuild-Rake.#{ezyBuild_version}/build"
Dir["#{ezyBuildDir}/*.rb"].each {|file| require file } # the order of referencing matters and is different under windows vs linux (TC)
Dir["#{ezyBuildDir}/Tools/*.rb"].each {|file| require file }
Dir["#{ezyBuildDir}/Projects/*.rb"].each {|file| require file }
Dir["./rake-build/*.rb"].each {|file| require file }

project_group = IdentityServerProjectGroup.new(root)
project = project_group.get_current_project()

task :build => [:clean_old_images] do
  project.build()
end

task :test do
  project.test()
end

task :push do
  project.push()
end

task :clean_old_images do
  project_group.clean_old_images()
end