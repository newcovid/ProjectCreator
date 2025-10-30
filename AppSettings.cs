// AppSettings.cs
using System.Collections.Generic;

namespace ProjectCreator
{
    /// <summary>
    /// 定义要保存在 config.json 文件中的设置
    /// </summary>
    public class AppSettings
    {
        // 模板的根文件夹路径
        public string TemplatePath { get; set; } = "";

        // 目标文件夹的基础路径
        public string TargetBasePath { get; set; } = "";

        // [新] 是否替换 README.md 文件中的占位符
        public bool ReplaceReadmeContent { get; set; } = true;

        // 用户自定义的占位符列表
        public List<CustomPlaceholder> UserPlaceholders { get; set; } = new();
    }

    /// <summary>
    /// 代表一个用户可配置的占位符
    /// </summary>
    public class CustomPlaceholder
    {
        /// <summary>
        /// 占位符的Key（不带%）。例如: "project_name"
        /// </summary>
        public string Key { get; set; } = "";

        /// <summary>
        /// 在UI中显示的标签。例如: "项目名称"
        /// </summary>
        public string Label { get; set; } = "";
    }
}