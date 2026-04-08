namespace HeosWpf.ViewModels.Navigation;

/// <summary>
/// 侧栏中「已保存设备」项的 <see cref="System.Windows.FrameworkElement.Tag"/> 载荷。
/// </summary>
public readonly record struct SavedDeviceNavTag(Guid Id, string Host);
