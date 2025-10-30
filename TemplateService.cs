// TemplateService.cs
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace ProjectCreator
{
    public class TemplateService
    {
        private readonly Dictionary<string, string> _variables;
        // [新] 添加一个字段来存储设置
        private readonly bool _replaceReadmeContent;

        // [已修改] 构造函数接收所有变量和 README.md 替换设置
        public TemplateService(Dictionary<string, string> variables, bool replaceReadmeContent)
        {
            _variables = variables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _replaceReadmeContent = replaceReadmeContent; // 存储设置
        }

        public void CreateProject(string resolvedSourceTemplatePath, string unresolvedTargetBasePath)
        {
            if (!Directory.Exists(resolvedSourceTemplatePath))
            {
                throw new DirectoryNotFoundException($"模板路径不存在: {resolvedSourceTemplatePath}");
            }

            string templateRootName = new DirectoryInfo(resolvedSourceTemplatePath).Name;
            string resolvedTargetBasePath = ResolveVariables(unresolvedTargetBasePath);
            Directory.CreateDirectory(resolvedTargetBasePath);

            string targetProjectName = ResolveVariables(templateRootName);
            string targetProjectFullPath = Path.Combine(resolvedTargetBasePath, targetProjectName);

            if (Directory.Exists(targetProjectFullPath))
            {
                throw new IOException($"错误：目标项目文件夹已存在！\n{targetProjectFullPath}");
            }

            CopyDirectory(resolvedSourceTemplatePath, targetProjectFullPath);
            ProcessDirectoryAndFiles(targetProjectFullPath);
        }

        private void ProcessDirectoryAndFiles(string path)
        {
            foreach (string subDir in Directory.GetDirectories(path))
            {
                ProcessDirectoryAndFiles(subDir);
            }

            foreach (string file in Directory.GetFiles(path))
            {
                ProcessFile(file);
            }

            string newPath = ResolveVariables(path);
            if (!string.Equals(newPath, path, StringComparison.Ordinal))
            {
                try
                {
                    Directory.Move(path, newPath);
                }
                catch (IOException ex)
                {
                    if (ex.Message.Contains("Access is denied") || ex.Message.Contains("拒绝访问"))
                    {
                        // 忽略
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        // 处理单个文件（重命名 + 内容替换）
        private void ProcessFile(string filePath)
        {
            // 1. 重命名文件
            string dir = Path.GetDirectoryName(filePath) ?? "";
            string oldName = Path.GetFileName(filePath);
            string newName = ResolveVariables(oldName);
            string newFilePath = Path.Combine(dir, newName);

            if (!string.Equals(newFilePath, filePath, StringComparison.Ordinal))
            {
                File.Move(filePath, newFilePath);
            }

            // 2. [已修改] 仅当开关打开且文件是 README.md 时才替换内容
            if (_replaceReadmeContent && newName.Equals("README.md", StringComparison.OrdinalIgnoreCase))
            {
                string content = File.ReadAllText(newFilePath, Encoding.UTF8);
                string newContent = ResolveVariables(content);
                File.WriteAllText(newFilePath, newContent, Encoding.UTF8);
            }
        }

        // ... CopyDirectory 和 ResolveVariables 方法保持不变 ...

        // 核心：递归复制目录
        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"源目录未找到: {dir.FullName}");

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        // 辅助方法：用字典中的值替换字符串中的占位符 (这是实例方法)
        public string ResolveVariables(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";

            return _variables.Aggregate(input, (current, variable) =>
                current.Replace(variable.Key, variable.Value, StringComparison.OrdinalIgnoreCase));
        }
    }
}