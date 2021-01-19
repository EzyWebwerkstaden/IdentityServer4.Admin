include Rake::DSL

class IdentityServerProjectGroup < ProjectGroup     
  def initialize(root) 
    super(root, "IdentityServer")
    sts_identity = DockerizedProject.new(
      project_group: self,
      name: 'IdentityServer4.STS.Identity',
      image_name: 'ezy.identityserver.sts.identity',
      build_dir: '',
      dockerfile_path: 'src/Skoruba.IdentityServer4.STS.Identity/Dockerfile')

    admin = DockerizedProject.new(
      project_group: self,
      name: 'IdentityServer4.Admin',
      image_name: 'ezy.identityserver.admin',
      build_dir: '',
      dockerfile_path: 'src/Skoruba.IdentityServer4.Admin/Dockerfile')

    @projects = [
      sts_identity,
      admin]
  end
end