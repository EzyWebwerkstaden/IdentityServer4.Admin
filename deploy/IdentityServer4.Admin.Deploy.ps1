# This version of Script requires ContainerDefinition.DependsOn property, which AWSPowerShell has since version 3.3.485
# Unfortunately, at the time of writing this script, Octopus' bundled version is 3.3.390 and does not have it.
# Octopus Process step must have the "Use AWS tools pre-installed on the worker" option selected
$CustomAWSPowerShellPath = $OctopusParameters["CustomAWSPowerShellPath"]
Write-Host "CustomAWSPowerShellPath: $CustomAWSPowerShellPath"
Import-Module -Name $CustomAWSPowerShellPath

$Region = $OctopusParameters["Octopus.Action.Aws.Region"]
$Environment = $OctopusParameters["Octopus.Environment.Name"]
$ListenerArn = $OctopusParameters["ListenerArn"]
$TargetGroupName = $OctopusParameters["TargetGroupName"]
$VpcSecurityGroup = $OctopusParameters["VpcSecurityGroup"]
$VpcSubnet = $OctopusParameters["VpcSubnet"].Split(",")
$VpcId = $OctopusParameters["VpcId"]
$HostName = $OctopusParameters["HostName"]
$LoadBalancerContainerPort = $OctopusParameters["LoadBalancerContainerPort"]
$AssignPublicIp = $OctopusParameters["AssignPublicIp"]
$ClusterName = $OctopusParameters["ClusterName"]
$ServiceName = $OctopusParameters["ServiceName"]
$ServiceDesiredCount = $OctopusParameters["ServiceDesiredCount"]
$ServiceMaximumPercent = $OctopusParameters["ServiceMaximumPercent"]
$ServiceMinimumPercent = $OctopusParameters["ServiceMinimumPercent"]
$PackageVersion = $OctopusParameters["Octopus.Release.Number"]
$GcrUrl = $OctopusParameters["GcrUrl"]
$GcrOrganization = $OctopusParameters["GcrOrganization"]
$GcrCredentialsSecretArn = $OctopusParameters["GcrCredentialsSecretArn"]
$ImageConfigName = $OctopusParameters["ImageConfigName"]
$ImageAppName = $OctopusParameters["ImageAppName"]
$ImageAppVersion = $OctopusParameters["ImageAppVersion"]

# Common
$GCRCredentials = New-Object -TypeName "Amazon.ECS.Model.RepositoryCredentials" -Property @{ CredentialsParameter=$GcrCredentialsSecretArn }
$AwsLogGroupName = "/ecs/$ServiceName-$Environment"
$LogConfigurationOptions = New-Object "System.Collections.Generic.Dictionary[String,String]"
$LogConfigurationOptions.Add("awslogs-region", $Region)
$LogConfigurationOptions.Add("awslogs-stream-prefix", "ecs")
$LogConfigurationOptions.Add("awslogs-group", $AwsLogGroupName)
$LogConfiguration = New-Object -TypeName "Amazon.ECS.Model.LogConfiguration" -Property @{ `
    LogDriver=[Amazon.ECS.LogDriver]::Awslogs; `
    Options=$LogConfigurationOptions;}
$Volumes = New-Object "System.Collections.Generic.List[Amazon.ECS.Model.Volume]"
$Volumes.Add($(New-Object -TypeName "Amazon.ECS.Model.Volume" -Property @{ Name="app-logbuffer"; }))
$Volumes.Add($(New-Object -TypeName "Amazon.ECS.Model.Volume" -Property @{ Name="app-config"; }))

# App
$AppImage = "$GcrUrl/$GcrOrganization/$($ImageAppName):$ImageAppVersion"
$AppPortMappings = New-Object "System.Collections.Generic.List[Amazon.ECS.Model.PortMapping]"
$AppPortMappings.Add($(New-Object -TypeName "Amazon.ECS.Model.PortMapping" -Property @{ HostPort=$LoadBalancerContainerPort; ContainerPort=$LoadBalancerContainerPort; Protocol=[Amazon.ECS.TransportProtocol]::Tcp}))
$AppEnvironmentVariables = New-Object "System.Collections.Generic.List[Amazon.ECS.Model.KeyValuePair]"
$AppEnvironmentVariables.Add($(New-Object -TypeName "Amazon.ECS.Model.KeyValuePair" -Property @{ Name="ASPNETCORE_ENVIRONMENT"; Value=$OctopusParameters["Environment"]}))
$AppEnvironmentVariables.Add($(New-Object -TypeName "Amazon.ECS.Model.KeyValuePair" -Property @{ Name="CORECLR_ENABLE_PROFILING"; Value=$OctopusParameters["CORECLR_ENABLE_PROFILING"]}))
$AppEnvironmentVariables.Add($(New-Object -TypeName "Amazon.ECS.Model.KeyValuePair" -Property @{ Name="ASPNETCORE_URLS"; Value="http://+:$LoadBalancerContainerPort"}))
$AppMountPoints = New-Object "System.Collections.Generic.List[Amazon.ECS.Model.MountPoint]"
$AppMountPoints.Add($(New-Object -TypeName "Amazon.ECS.Model.MountPoint" -Property @{ SourceVolume="app-logbuffer"; ContainerPath="/logbuffer"}))
$AppMountPoints.Add($(New-Object -TypeName "Amazon.ECS.Model.MountPoint" -Property @{ SourceVolume="app-config"; ContainerPath="/app/CustomSettings"}))
$AppDependencies = New-Object "System.Collections.Generic.List[Amazon.ECS.Model.ContainerDependency]"
$AppDependencies.Add($(New-Object -TypeName "Amazon.ECS.Model.ContainerDependency" -Property @{ ContainerName="config"; Condition="START"}))

Write-Host "Adding Container Definition for $AppImage"
$ContainerDefinitions = New-Object "System.Collections.Generic.List[Amazon.ECS.Model.ContainerDefinition]"
$ContainerDefinitions.Add($(New-Object -TypeName "Amazon.ECS.Model.ContainerDefinition" -Property @{ `
    Name="app"; `
    Image=$AppImage; `
    PortMappings=$AppPortMappings; `
    MountPoints=$AppMountPoints; `
    LogConfiguration=$LogConfiguration; `
    Essential=$true; `
    DependsOn=$AppDependencies; `
    RepositoryCredentials=$GCRCredentials; `
    Environment=$AppEnvironmentVariables;}))

# config
$ConfigImage = "$GcrUrl/$GcrOrganization/$($ImageConfigName):$PackageVersion"
$ConfigMountPoints = New-Object "System.Collections.Generic.List[Amazon.ECS.Model.MountPoint]"
$ConfigMountPoints.Add($(New-Object -TypeName "Amazon.ECS.Model.MountPoint" -Property @{ SourceVolume="app-config"; ContainerPath="/Mount"}))

Write-Host "Adding Container Definition for $ConfigImage"
$ContainerDefinitions.Add($(New-Object -TypeName "Amazon.ECS.Model.ContainerDefinition" -Property @{ `
    Name="config"; `
    Image=$ConfigImage; `
    MountPoints=$ConfigMountPoints; `
    LogConfiguration=$LogConfiguration; `
    Essential=$false; `
    RepositoryCredentials=$GCRCredentials;}))



# Create Task
$TaskName = $OctopusParameters["TaskName"]
$ExecutionRole = $OctopusParameters["AwsArn"]
$TaskCpu = $OctopusParameters["TaskCpu"]
$TaskMemory = $OctopusParameters["TaskMemory"]

Write-Host "Creating New Task Definition $TaskName"
$TaskDefinitionResponse = Register-ECSTaskDefinition `
    -ContainerDefinition $ContainerDefinitions `
    -Cpu $TaskCpu `
    -Family $TaskName `
    -TaskRoleArn $ExecutionRole `
    -ExecutionRoleArn $ExecutionRole `
    -Memory $TaskMemory `
    -NetworkMode awsvpc `
    -Region $Region `
    -Volume $Volumes `
    -RequiresCompatibility "FARGATE"

if(!$?) {
    Write-Error "Failed to register new task definition"
    Exit 0
}

Write-Host "Created Task Definition:"
Write-Verbose $($TaskDefinitionResponse | ConvertTo-Json)
$TaskDefinitionArn = $TaskDefinitionResponse.TaskDefinition.TaskDefinitionArn
Write-Host "New TaskDefinitionArn: $TaskDefinitionArn"
$LogGroup = Get-CWLLogGroup -LogGroupNamePrefix $AwsLogGroupName -Region $Region | Select-Object -First 1

if(!$LogGroup) {
	Write-Host "Creating CloudWatch log group $AwsLogGroupName"
	New-CWLLogGroup `
      -LogGroupName $AwsLogGroupName `
      -Region $Region `
      -Force
}

# Update/Create Service
# Check if service exists
$Service = Get-ECSService `
    -Cluster $ClusterName `
    -Service $ServiceName `
    -Region $Region `

if(!$?) {
    Write-Error "Failed to get service"
}

Write-Host "Current service - $ServiceName"
Write-Verbose $($Service | ConvertTo-Json)

if(($Service.Services | Measure-Object).Count -gt 0 -and $Service.Services[0].Status -ne "INACTIVE") {
  Write-Host "Service $ServiceName exists. Updating Service."

  $ServiceUpdate = Update-ECSService `
      -Cluster $ClusterName `
      -ForceNewDeployment $true `
      -AwsvpcConfiguration_AssignPublicIp $AssignPublicIp `
      -AwsvpcConfiguration_SecurityGroup $VpcSecurityGroup `
      -Service $ServiceName `
      -AwsvpcConfiguration_Subnet $VpcSubnet `
      -TaskDefinition $TaskDefinitionArn `
      -Region $Region `
      -DesiredCount $ServiceDesiredCount `
      -DeploymentConfiguration_MaximumPercent $ServiceMaximumPercent `
      -DeploymentConfiguration_MinimumHealthyPercent $ServiceMinimumPercent `
      -Force

  if(!$?) {
      Write-Error "Failed to update service"
      Exit 0
  }
  Write-Host "Updated Service $($ServiceUpdate.ServiceArn)"
  Write-Verbose $($ServiceUpdate | ConvertTo-Json)
}
else {
  Write-Host "Service $ServiceName does not exist. Will create new security group, target group and service."

  $TargetGroup = $null
  Try {
    $TargetGroups = Get-ELB2TargetGroup `
    -Name $TargetGroupName

    Write-Verbose $($TargetGroups | ConvertTo-Json)

    if(!$?) {
      Write-Error "Failed to get target group"
    }
    else{
      if(($TargetGroups | Measure-Object).Count -gt 0) {
        $TargetGroup = $TargetGroups[0]
      }
    }
  }
  Catch {
    Write-Host "Could not find target group, will create new"
  }

  if(!$TargetGroup) {
    Write-Host "Creating new target group $TargetGroupName"
    $TargetGroup = New-ELB2TargetGroup `
      -Name $TargetGroupName `
      -Port $LoadBalancerContainerPort `
      -Protocol "HTTP" `
      -Matcher_HttpCode "200,302" `
      -TargetType "ip" `
      -VpcId $VpcId `
      -Region $Region `
      -Force

    Write-Host "Adding tg attributes to tg arn: $($TargetGroup.TargetGroupArn)"
    $TGAttributes = New-Object "System.Collections.Generic.List[Amazon.ElasticLoadBalancingV2.Model.TargetGroupAttribute]"
    $TGAttributes.Add($(New-Object -TypeName "Amazon.ElasticLoadBalancingV2.Model.TargetGroupAttribute" -Property @{ Key="deregistration_delay.timeout_seconds"; Value=10}))
    Edit-ELB2TargetGroupAttribute `
      -TargetGroupArn $TargetGroup.TargetGroupArn `
      -Attribute $TGAttributes `
      -Force
  }

  if($HostName) {
    Write-Host "Getting appropiate Priorty for ELB2 rule"
    $Rules = Get-ELB2Rule `
      -ListenerArn $ListenerArn `
      -Region $Region

    $ExistingRule = $Rules | Where { $_.Actions | Where ({ $_.TargetGroupArn -eq $TargetGroup.TargetGroupArn } | Measure-Object).Count -gt 0 } | Select-Object -First 1

    if(!$ExistingRule) {
      $HighestRulePriority = $Rules |
        Select-Object *, @{ n = "IntPriority"; e = { [int]($_.Priority) } } |
        Sort-Object IntPriority |
        Select-Object -Last 1

      $RulePriority = $HighestRulePriority.IntPriority + 1

      $Actions = New-Object "System.Collections.Generic.List[Amazon.ElasticLoadBalancingV2.Model.Action]"
      $Actions.Add($(New-Object -TypeName "Amazon.ElasticLoadBalancingV2.Model.Action" -Property @{ TargetGroupArn=$TargetGroup.TargetGroupArn; Type="forward" }))
      $Conditions = New-Object "System.Collections.Generic.List[Amazon.ElasticLoadBalancingV2.Model.RuleCondition]"
      $Conditions.Add($(New-Object -TypeName "Amazon.ElasticLoadBalancingV2.Model.RuleCondition" -Property @{ Field="host-header"; Values=$HostName }))

      Write-Host "Creating new ELB2 rule for hostname $HostName"
      $AddElbRule = New-ELB2Rule `
        -ListenerArn $ListenerArn `
        -Action $Actions `
        -Condition $Conditions `
        -Priority $RulePriority `
        -Region $Region `
        -Force
    }
  }

  $LoadBalancers = New-Object "System.Collections.Generic.List[Amazon.ECS.Model.LoadBalancer]"

  if($LoadBalancerContainerPort) {
    $LoadBalancers.Add($(New-Object -TypeName "Amazon.ECS.Model.LoadBalancer" -Property @{ ContainerName="app"; ContainerPort=$LoadBalancerContainerPort; TargetGroupArn=$TargetGroup.TargetGroupArn; }))
  }

  Write-Host "Creating new service $ServiceName"

  $NewService = New-ECSService `
    -Cluster $ClusterName `
    -AwsvpcConfiguration_AssignPublicIp $AssignPublicIp `
    -DesiredCount $ServiceDesiredCount `
    -LaunchType "FARGATE" `
    -LoadBalancer $LoadBalancers `
    -DeploymentConfiguration_MaximumPercent $ServiceMaximumPercent `
    -DeploymentConfiguration_MinimumHealthyPercent $ServiceMinimumPercent `
    -AwsvpcConfiguration_SecurityGroup $VpcSecurityGroup `
    -ServiceName $ServiceName `
    -AwsvpcConfiguration_Subnet $VpcSubnet `
    -TaskDefinition $TaskDefinitionArn `
    -Region $Region `
    -HealthCheckGracePeriodSecond 45 `
    -Force

  if(!$?) {
    Write-Error "Failed to create service"
    Exit 0
  }
  Write-Host "Created Service $($NewService.ServiceArn)"
  Write-Verbose $($NewService | ConvertTo-Json)
}
