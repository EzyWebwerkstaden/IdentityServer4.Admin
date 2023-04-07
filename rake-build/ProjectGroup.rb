include Rake::DSL

class ProjectGroup < ProjectGroupBase
  def initialize(root, tools_group)
    super(root, "IdentityServer")
    sts_identity = DockerizedProject.new(
      project_group: self,
      name: 'IdentityServer4.STS.Identity',
      build_dir: '',
      dockerfile_path: 'src/Skoruba.IdentityServer4.STS.Identity/Dockerfile',
      image_name: 'ezy.identityserver.sts.identity',
      attributes: {
        'secrets_build_param' => "--secret id=gcr-pat,env=GCR_PAT \ ",
        'nexus' => {
          'docker_image_path' => 'sabre/radixx/ezy/ezy-identityserver-sts-identity'
        }
      })

    admin = DockerizedProject.new(
      project_group: self,
      name: 'IdentityServer4.Admin',
      build_dir: '',
      dockerfile_path: 'src/Skoruba.IdentityServer4.Admin/Dockerfile',
      image_name: 'ezy.identityserver.admin',
      attributes: {
        'secrets_build_param' => "--secret id=gcr-pat,env=GCR_PAT \ ",
        'nexus' => {
          'docker_image_path' => 'sabre/radixx/ezy/ezy-identityserver-admin'
        }
      })

    @projects = [sts_identity, admin]
  end
end
