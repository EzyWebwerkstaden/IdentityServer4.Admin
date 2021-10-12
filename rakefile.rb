
require 'rexml/document'
include REXML
root = File.dirname(__FILE__)

packages_xml = Document.new(File.open('packages.config'))
ezyBuild_version = XPath.first(packages_xml, "/packages/package[@id='EzyWebwerkstaden.ezyBuild-Rake']").attributes.get_attribute("version").value
ezyBuildDir = "./packages/EzyWebwerkstaden.ezyBuild-Rake.#{ezyBuild_version}/build"
if (!File.exists?(ezyBuildDir))
  puts "fetching ezyBuild-Rake version: #{ezyBuild_version}"
  # Fetch with Mono image which contains nuget. Nuget is easier to work with non-dotnet packages than dotnet restore.
  sh "docker run --rm -v #{root}:/app mono:6.12.0.107 /bin/sh -c \" \
      cd /app && \
      nuget sources add -name 'github' -source 'https://nuget.pkg.github.com/ezywebwerkstaden/index.json' -username 'ezydeploy' -password #{ENV["GCR_PAT"]} && \
      nuget restore ./packages.config -PackagesDirectory ./packages\""
elsif
  puts "Skip fetching ezyBuild-Rake version: #{ezyBuild_version}, package already downloaded"
end

Dir["#{ezyBuildDir}/*.rb"].each {|file| require file } # the order of referencing matters and is different under windows vs linux (TC)
Dir["#{ezyBuildDir}/Tools/*.rb"].each {|file| require file }
Dir["#{ezyBuildDir}/Projects/*.rb"].each {|file| require file }
Dir["./rake-build/*.rb"].each {|file| require file }
tools_group = ToolsGroup.new(root)
project_group = ProjectGroup.new(root, tools_group)
project = nil
tool = nil

task :require_project do
  project = project_group.get_current_project()
end

task :require_tool do
  tool = tools_group.get_current_tool()
end

task :build => [:require_project] do
  project.build()
end

task :pack => [:require_project] do
  project.pack()
end

task :test => [:require_project] do
  project.test()
end

task :code_coverage => [:require_project] do
  project.code_coverage()
end

task :push => [:require_project] do
  project.push()
end

task :run => [:require_project, :require_tool] do
  tool.run(project)
end

task :detect_feature_site do
  project_group.detect_feature_site()
end

