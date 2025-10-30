namespace ProjectCreator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>

    // [已修正] 明确指定使用 System.Windows.Application 来消除歧义
    // 这个修正也解决了您在上一轮中遇到的 "Application" 不明确的错误
    public partial class App : System.Windows.Application
    {
    }
}

