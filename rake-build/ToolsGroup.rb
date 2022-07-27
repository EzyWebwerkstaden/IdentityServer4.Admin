include Rake::DSL

class ToolsGroup < ToolsGroupBase
  def initialize(root)
    super(root)

    docker_push_from_github_to_nexus = TransferDockerImage.new(
      name: 'docker-push-from-github-to-nexus',
      tools_group: self,
      source_registry: DockerRegistry.github_ezy,
      target_registry: DockerRegistry.sabre_nexus,
      image_map: {
        'ezy.identityserver.sts.identity' => 'ezy-identityserver-sts-identity',
        'ezy.identityserver.admin' => 'ezy-identityserver-admin',
      })

    @tools = [docker_push_from_github_to_nexus]
  end
end
