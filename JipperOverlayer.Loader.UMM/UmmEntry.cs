using System;
using System.IO;
using UnityModManagerNet;

namespace JipperOverlayer.Loader;

public static class UmmEntry
{
    public static bool Load(UnityModManager.ModEntry entry)
    {
        Main.Init(new UmmHandler(entry));
        return true;
    }
}

internal class UmmHandler : IModLoader
{
    private readonly UnityModManager.ModEntry _entry;
    private readonly UnityModManager.ModEntry.ModLogger _logger;

    public string ModPath => _entry.Path;

    public UmmHandler(UnityModManager.ModEntry entry)
    {
        _entry = entry;
        _logger = entry.Logger;

        // Bridge UMM's ModEntry-param events → simple events
        entry.OnUpdate += OnUpdateBridge;
        entry.OnToggle += OnToggleBridge;
        entry.OnGUI += OnGUIBridge;
        entry.OnSaveGUI += OnSaveGUIBridge;
    }

    public void Log(string msg) => _logger.Log(msg);
    public void Warning(string msg) => _logger.Warning(msg);
    public void Error(string msg) => _logger.Error(msg);

    // UMM 的 OnToggle 返回 true 不会因为 Main 初始化失败自动重试；保留请求状态，
    // 由 OnUpdate 以退避间隔重试，和 MelonLoader 的 Main.IsEnabled 回写行为一致。
    private bool _requestedEnabled;
    private DateTime _nextEnableRetry = DateTime.MinValue;

    // UMM event signatures carry ModEntry — we ignore it and forward the value
    private void OnUpdateBridge(UnityModManager.ModEntry _, float dt)
    {
        OnUpdateEvent?.Invoke(dt);
        if (_requestedEnabled && !Main.IsEnabled && DateTime.UtcNow >= _nextEnableRetry)
        {
            _nextEnableRetry = DateTime.UtcNow.AddSeconds(5);
            OnToggleEvent?.Invoke(true);
        }
    }

    private bool OnToggleBridge(UnityModManager.ModEntry _, bool v)
    {
        _requestedEnabled = v;
        _nextEnableRetry = DateTime.MinValue;
        OnToggleEvent?.Invoke(v);
        return true;
    }
    private void OnGUIBridge(UnityModManager.ModEntry _) => OnGUIEvent?.Invoke();
    private void OnSaveGUIBridge(UnityModManager.ModEntry _) => OnSaveGUIEvent?.Invoke();

    // Backing events for IModLoader
    private event Action<float> OnUpdateEvent;
    private event Action<bool> OnToggleEvent;
    private event Action OnGUIEvent;
    private event Action OnSaveGUIEvent;

    public event Action<float> OnUpdate
    {
        add => OnUpdateEvent += value;
        remove => OnUpdateEvent -= value;
    }

    public event Action<bool> OnToggle
    {
        add => OnToggleEvent += value;
        remove => OnToggleEvent -= value;
    }

    public event Action OnGUI
    {
        add => OnGUIEvent += value;
        remove => OnGUIEvent -= value;
    }

    public event Action OnSaveGUI
    {
        add => OnSaveGUIEvent += value;
        remove => OnSaveGUIEvent -= value;
    }
}
