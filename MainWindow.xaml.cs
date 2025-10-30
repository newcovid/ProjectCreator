// MainWindow.xaml.cs
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectCreator
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings = new AppSettings();
        private const string ConfigFileName = "config.json";

        private List<RuntimeInput> _runtimeInputs = new();

        private static string GetConfigFilePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string configDir = Path.Combine(appDataPath, "ProjectCreator");
            Directory.CreateDirectory(configDir);
            return Path.Combine(configDir, ConfigFileName);
        }

        private readonly string _configFilePath = GetConfigFilePath();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            UpdateUIFromSettings();
        }

        /// <summary>
        /// 根据加载的 _settings 更新 UI 绑定
        /// </summary>
        private void UpdateUIFromSettings()
        {
            // 1. 更新路径
            TxtTemplatePath.Text = _settings.TemplatePath;
            TxtTargetPath.Text = _settings.TargetBasePath;

            // 2. 更新高级配置中的 DataGrid
            ConfigDataGrid.ItemsSource = null;
            ConfigDataGrid.ItemsSource = _settings.UserPlaceholders;

            // 3. 更新主界面的动态输入框
            _runtimeInputs = _settings.UserPlaceholders
                .Select(p => new RuntimeInput
                {
                    Key = $"%{p.Key}%",
                    Label = p.Label,
                    Value = ""
                })
                .ToList();

            DynamicInputsControl.ItemsSource = null;
            DynamicInputsControl.ItemsSource = _runtimeInputs;

            // 4. [新] 更新 CheckBox
            ChkReplaceReadme.IsChecked = _settings.ReplaceReadmeContent;
        }

        // --- 核心创建逻辑 ---
        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            ClearStatus();

            // 1. 验证动态输入
            foreach (var input in _runtimeInputs)
            {
                if (string.IsNullOrWhiteSpace(input.Value))
                {
                    ShowError($"输入项 '{input.Label}' 不能为空！");
                    return;
                }
            }

            // 2. 验证配置路径
            var presetVars = PlaceholderService.GetPresetVariables();
            string resolvedTemplatePathWithPresets = PlaceholderService.Resolve(_settings.TemplatePath, presetVars);

            if (!Directory.Exists(resolvedTemplatePathWithPresets))
            {
                if (!Directory.Exists(_settings.TemplatePath))
                {
                    ShowError($"模板路径无效: {resolvedTemplatePathWithPresets} (或 {_settings.TemplatePath})");
                    return;
                }
                resolvedTemplatePathWithPresets = _settings.TemplatePath;
            }

            if (string.IsNullOrWhiteSpace(_settings.TargetBasePath))
            {
                ShowError("目标路径无效。请展开'高级配置'设置路径。");
                return;
            }

            try
            {
                // 3. 准备所有变量 (预设 + 用户输入)
                var allVariables = PlaceholderService.GetPresetVariables();
                var userVariables = _runtimeInputs.ToDictionary(
                    i => i.Key,
                    i => i.Value.Trim(),
                    StringComparer.OrdinalIgnoreCase
                );

                foreach (var userVar in userVariables)
                {
                    allVariables[userVar.Key] = userVar.Value;
                }

                // 4. 执行创建
                // [已修改] 传入 ReplaceReadmeContent 设置
                var service = new TemplateService(allVariables, _settings.ReplaceReadmeContent);

                service.CreateProject(resolvedTemplatePathWithPresets, _settings.TargetBasePath);

                // 5. 成功
                ShowSuccess("项目创建成功！");
                foreach (var input in _runtimeInputs)
                {
                    input.Value = "";
                }
                DynamicInputsControl.ItemsSource = null;
                DynamicInputsControl.ItemsSource = _runtimeInputs;

                if (DynamicInputsControl.Items.Count > 0)
                {
                    var container = DynamicInputsControl.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
                    if (container != null)
                    {
                        var textBox = FindVisualChild<System.Windows.Controls.TextBox>(container);
                        textBox?.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                // 6. 失败
                ShowError($"创建失败: {ex.Message}");
            }
        }

        // --- 配置与浏览 ---

        private void BtnBrowseTemplate_Click(object sender, RoutedEventArgs e)
        {
            string? selectedPath = ShowFolderBrowser("请选择模板的【根】文件夹", TxtTemplatePath.Text);
            if (selectedPath != null)
            {
                TxtTemplatePath.Text = selectedPath;
                ShowStatus("路径已更新。请手动添加占位符（如果需要）并点击'保存配置'。");
            }
        }

        private void BtnBrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            string? selectedPath = ShowFolderBrowser("请选择项目创建的【目标基础】路径", TxtTargetPath.Text);
            if (selectedPath != null)
            {
                TxtTargetPath.Text = selectedPath;
                ShowStatus("路径已更新。请手动添加占位符（例如 \\%year%\\%month%）并点击'保存配置'。");
            }
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateAndSaveSettings())
            {
                UpdateUIFromSettings();
                ShowSuccess("配置已保存。");
            }
        }

        private string? ShowFolderBrowser(string description, string initialPath)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = true;

                string resolvedInitialPath = PlaceholderService.Resolve(initialPath, PlaceholderService.GetPresetVariables());

                if (Directory.Exists(resolvedInitialPath))
                {
                    dialog.SelectedPath = resolvedInitialPath;
                }
                else if (Directory.Exists(initialPath))
                {
                    dialog.SelectedPath = initialPath;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
            }
            return null;
        }

        // --- 配置持久化 (JSON) ---

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    string json = File.ReadAllText(_configFilePath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    // [已修改] AppSettings 中 ReplaceReadmeContent 默认为 true，
                    // 因此反序列化时如果 JSON 中缺少该字段，它将自动保持 true，
                    // 这正是我们期望的（保持对旧配置的兼容性）。

                    // ... (迁移逻辑)
                    if (_settings.UserPlaceholders.Count == 0 &&
                        _settings.TemplatePath.Contains("%wl_contract_no%"))
                    {
                        _settings.UserPlaceholders.Add(new CustomPlaceholder { Key = "contract_no", Label = "合同号(_C):" });
                        _settings.UserPlaceholders.Add(new CustomPlaceholder { Key = "project_name", Label = "项目名称(_P):" });
                        _settings.UserPlaceholders.Add(new CustomPlaceholder { Key = "order_no", Label = "订单号(_O):" });
                    }
                }
                else
                {
                    // [已修改] 确保新配置中包含 ReplaceReadmeContent（虽然默认为true，但显式设置更清晰）
                    _settings = new AppSettings
                    {
                        TemplatePath = @"D:\订单模版\版本V2.1\[%project_name%]",
                        TargetBasePath = @"D:\项目输出\%year%\%month%",
                        ReplaceReadmeContent = true,
                        UserPlaceholders = new List<CustomPlaceholder>
                        {
                            new CustomPlaceholder { Key = "project_name", Label = "项目名称:" },
                            new CustomPlaceholder { Key = "order_no", Label = "订单号:" }
                        }
                    };
                    ValidateAndSaveSettings(true);
                }
            }
            catch (Exception ex)
            {
                ShowError($"加载配置失败: {ex.Message}");
                _settings = new AppSettings();
            }
        }

        private bool ValidateAndSaveSettings(bool isSilent = false)
        {
            try
            {
                _settings.TemplatePath = TxtTemplatePath.Text.Trim();
                _settings.TargetBasePath = TxtTargetPath.Text.Trim();
                // [新] 保存 CheckBox 的值
                _settings.ReplaceReadmeContent = ChkReplaceReadme.IsChecked ?? true;

                // ... (验证自定义占位符)
                var presetKeys = PlaceholderService.GetPresetKeys();
                var keyRegex = new Regex(@"^[a-zA-Z0-9_]+$");
                var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var ph in _settings.UserPlaceholders)
                {
                    // ... (验证逻辑)
                    if (string.IsNullOrWhiteSpace(ph.Key))
                    {
                        ShowError("配置错误：占位符的 'Key' 不能为空。");
                        return false;
                    }
                    if (!keyRegex.IsMatch(ph.Key))
                    {
                        ShowError($"配置错误：Key '{ph.Key}' 包含无效字符。只能使用字母、数字和下划线。");
                        return false;
                    }
                    if (string.IsNullOrWhiteSpace(ph.Label))
                    {
                        ShowError($"配置错误：Key '{ph.Key}' 必须有一个 'UI 标签'。");
                        return false;
                    }
                    string fullKey = $"%{ph.Key}%";
                    if (presetKeys.Contains(fullKey))
                    {
                        ShowError($"配置错误：Key '{ph.Key}' 与预设占位符（如 %year%, %day% 等）冲突。");
                        return false;
                    }
                    if (!usedKeys.Add(ph.Key))
                    {
                        ShowError($"配置错误：Key '{ph.Key}' 重复定义。");
                        return false;
                    }
                }

                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(_configFilePath, json);

                if (!isSilent)
                {
                }
                return true;
            }
            catch (Exception ex)
            {
                ShowError($"保存配置失败: {ex.Message}");
                return false;
            }
        }

        // --- 状态显示辅助 ---
        private void ShowError(string message)
        {
            TxtStatus.Text = message;
            TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
        }

        private void ShowSuccess(string message)
        {
            TxtStatus.Text = message;
            TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
        }

        private void ShowStatus(string message)
        {
            TxtStatus.Text = message;
            TxtStatus.Foreground = System.Windows.Media.Brushes.Gray;
        }

        private void ClearStatus()
        {
            TxtStatus.Text = "";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Black;
        }

        // --- 辅助方法 ---
        private static T? FindVisualChild<T>(DependencyObject? obj) where T : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t) return t;
                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }
    }

    public class RuntimeInput
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
    }
}