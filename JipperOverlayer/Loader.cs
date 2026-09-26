using System;
using System.IO;
using System.Text;

namespace JipperOverlayer;

public static class Loader
{
    public static IModLoader Instance { get; internal set; }

    public static string ModPath => Instance?.ModPath ?? ".";
    public static void Log(string m) => Instance?.Log(m);
    public static void Warning(string m) => Instance?.Warning(m);
    public static void Error(string m) => Instance?.Error(m);

    /// <summary>原子写文本：先写 .tmp，再同卷替换目标，保留 .bak 备份。
    /// 供设置/颜色/标签等 JSON 持久化共用，避免崩溃或断电留下截断文件。</summary>
    public static void WriteAllTextAtomic(string path, string contents)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, contents ?? string.Empty, new UTF8Encoding(false));
        if (File.Exists(path))
        {
            try
            {
                File.Replace(tmp, path, path + ".bak", true);
                return;
            }
            catch (PlatformNotSupportedException) { }
            catch (NotSupportedException) { }
            catch (IOException) { }
            string backup = path + ".bak";
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
            try { File.Move(tmp, path); }
            catch
            {
                // 替换失败时尽力把主文件放回原位，至少保留可读的主/备份之一。
                if (!File.Exists(path) && File.Exists(backup)) File.Move(backup, path);
                throw;
            }
            return;
        }
        File.Move(tmp, path);
    }

    public static event Action<float> OnUpdate
    {
        add => Instance.OnUpdate += value;
        remove => Instance.OnUpdate -= value;
    }
    public static event Action<bool> OnToggle
    {
        add => Instance.OnToggle += value;
        remove => Instance.OnToggle -= value;
    }
    public static event Action OnGUI
    {
        add => Instance.OnGUI += value;
        remove => Instance.OnGUI -= value;
    }
    public static event Action OnSaveGUI
    {
        add => Instance.OnSaveGUI += value;
        remove => Instance.OnSaveGUI -= value;
    }
}
