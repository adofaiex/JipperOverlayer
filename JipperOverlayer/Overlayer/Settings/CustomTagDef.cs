namespace JipperOverlayer.Overlayer;

/// <summary>
/// 用户自定义的组合 tag：Name = 模板里写的名字（仅限字母/数字/下划线），
/// Template = 展开的模板片段（可引用其他 tag，含自定义组合 tag——注意别成环）。
/// 注册入口：TagManager.SetCustomTags；设置面板「自定义 tag」分区可视化编辑。
/// </summary>
public class CustomTagDef
{
    public string Name = "";
    public string Template = "";
}
