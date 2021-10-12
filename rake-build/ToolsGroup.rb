include Rake::DSL

class ToolsGroup < ToolsGroupBase
  def initialize(root)
    super(root)

    @tools = []
  end
end
