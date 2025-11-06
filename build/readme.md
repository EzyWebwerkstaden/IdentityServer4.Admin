# ezyBuild 
## Overview
This project uses the ezyBuild framework from the [ezyDevOps repository](https://github.com/EzyWebwerkstaden/ezyDevOps) as the CI engine, that is used by [TeamCity](https://ezy-teamcity-server-radixx-ezyc-dev-ops.apps.dev-01.us-central1.dev.sabre-gcp.com/). You can also use it locally to replicate all steps that are run under TeamCity. Helpful links:
* [ezyDevOps project](https://github.com/EzyWebwerkstaden/ezyDevOps)
* [ezyBuild overview & setup](https://github.com/EzyWebwerkstaden/ezyDevOps/blob/master/src/ezyBuild/readme.md) - shows how to start with working with ezyBuild.
* [ezyBuild commands](https://github.com/EzyWebwerkstaden/ezyDevOps/blob/master/src/ezyBuild/.md/commands.md) - explains available commands.

## Sample commands
Once you are in your local agent container, you can run e.g. 
~~~
* rake build PROJECT=<your-project> BUILD_NUMBER=0.0.1
~~~
To find out what are the available projects, please look into [ProjectGroup.rb](ProjectGroup.rb), locate the project you wish to build and note its 'name' property value. This will be the value that you fill into the PROJECT environment variable in the command above. Please keep in mind other necessary environment variables that are needed in certain scenarios, like e.g. DOCKERFILE_TARGET (for building gcp or aws images) or REGISTRY (to determine the target registry for the push commands).

## Command reference
We shouldn't be building the entire command reference per-project in such readme, as it would quickly become stale. Instead, always refer to what TeamCity uses and use this as the source of truth. 

To illustrate where to find necessary information, go to TeamCity, locate the project and the build that performs the action you wish to check. Then switch to edit mode and locate the build step, like below:
![build step](https://github.com/EzyWebwerkstaden/ezyDevOps/blob/master/src/ezyBuild/.md/template-buildstep-commands.png)

It's also good to check which environment variables are passed on the build, folder or project level in TeamCity. ![parameters](https://github.com/EzyWebwerkstaden/ezyDevOps/blob/master/src/ezyBuild/.md/template-build-parameters.png)

## Updating ezyBuild
* locate the ezyBuild version number that was built on [TeamCity](https://ezy-teamcity-server-radixx-ezyc-dev-ops.apps.dev-01.us-central1.dev.sabre-gcp.com/buildConfiguration/EzyContainer_EzyDevOps_EzyBuild#all-projects).
* make sure the package is correctly uploaded to GitHub's package repository and you can find in [ezyBuild nuget repository](https://github.com/EzyWebwerkstaden/ezyDevOps/pkgs/nuget/ezyBuild).
* update the [.env file](.env) with the new version.
* either starting fresh agent container or running any rake command inside it will pull the new ezyBuild version to your local [packages/ezyBuild folder](../packages/ezybuild/).
