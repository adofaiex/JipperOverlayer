using System;
using System.IO;
using System.Reflection;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(JipperOverlayer.Melon.JipperMelonMod), "Jipper Overlayer", "1.1.5", "HitMargin", null)]
[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]

namespace JipperOverlayer.Melon;

public class JipperMelonMod : MelonMod
{
    private static MelonHandler _handler;
    private static bool _enabled;

    public override void OnInitializeMelon()
    {
        _handler = new MelonHandler();
        Main.Init(_handler);

        // Register preferences
        var cat = MelonPreferences.CreateCategory("JipperOverlayer", "Jipper Overlayer");
        SettingsWindow.Hotkey = cat.CreateEntry("Hotkey", KeyCode.F7, "Settings Hotkey");
        SettingsWindow.Enabled = cat.CreateEntry("ModEnabled", true, "Mod Enabled");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        if (_enabled) return;
        // 尊重 ModEnabled 首选项：关闭时启动不自动启用，等用户打开后再经 OnUpdate 启用。
        if (SettingsWindow.Enabled != null && !SettingsWindow.Enabled.Value) return;
        _enabled = true;
        Main.Enable();
    }

    public override void OnUpdate()
    {
        // Hotkey check
        if (Input.GetKeyDown(SettingsWindow.Hotkey.Value))
            SettingsWindow.ToggleVisible();

        // 跟随 ModEnabled 首选项实时启停（MelonLoader 没有自己的 OnToggle）。
        bool wantEnabled = SettingsWindow.Enabled == null || SettingsWindow.Enabled.Value;
        if (wantEnabled && !_enabled)
        {
            _enabled = true;
            Main.Enable();
        }
        else if (!wantEnabled && _enabled)
        {
            _enabled = false;
            Main.Disable();
        }

        _handler?.TriggerOnUpdate(Time.deltaTime);
    }

    public override void OnGUI()
    {
        SettingsWindow.Draw();
    }

    public override void OnApplicationQuit()
    {
        if (_enabled)
            Main.Disable();
    }
}

internal static class SettingsWindow
{
    public static MelonPreferences_Entry<KeyCode> Hotkey;
    public static MelonPreferences_Entry<bool> Enabled;

    private static Rect _windowRect = new(100, 100, 600, 400);
    private static bool _visible;
    private static readonly int _windowId = "JipperOverlayerSettings".GetHashCode();
    private static Vector2 _scrollPos;
    private static bool _awaitingKey;

    public static void ToggleVisible()
    {
        _awaitingKey = false;
        _visible = !_visible;
    }

    public static void Draw()
    {
        if (!_visible) return;

        // Dark overlay background
        var bgColor = new Color(0, 0, 0, 0.7f);
        GUI.color = bgColor;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        _windowRect = GUILayout.Window(_windowId, _windowRect, DrawWindow, "Jipper Overlayer Settings",
            GUILayout.MinWidth(450), GUILayout.MaxWidth(800));

        if (Enabled != null && !Enabled.Value)
        {
            _visible = false;
            _awaitingKey = false;
            MelonPreferences.Save();
        }
    }

    private static void DrawWindow(int id)
    {
        if (Main.Settings == null)
        {
            GUILayout.Label("Settings not loaded.");
            return;
        }

        // Hotkey rebinding
        GUILayout.BeginHorizontal();
        GUILayout.Label("Settings Hotkey: ", GUILayout.Width(120));
        string btnLabel = _awaitingKey ? "[ Press any key... ]" : $" {Hotkey.Value} ";
        if (GUILayout.Button(btnLabel, GUILayout.Width(180), GUILayout.Height(22)))
        {
            _awaitingKey = true;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (_awaitingKey && Event.current != null && Event.current.isKey && Event.current.keyCode != KeyCode.None)
        {
            Hotkey.Value = Event.current.keyCode;
            MelonPreferences.Save();
            _awaitingKey = false;
            Event.current.Use();
        }

        GUILayout.Space(5);

        _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);

        GUILayout.Label("-- Mod Settings --", GUILayout.ExpandWidth(true));
        Main.Settings.OnGUI();

        GUILayout.EndScrollView();

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save && Close", GUILayout.Height(30), GUILayout.MinWidth(120)))
        {
            Main.Settings.OnSaveGUI();
            MelonPreferences.Save();
            _visible = false;
            _awaitingKey = false;
        }
        if (GUILayout.Button("Close", GUILayout.Height(30), GUILayout.MinWidth(80)))
        {
            _visible = false;
            _awaitingKey = false;
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUI.DragWindow(new Rect(0, 0, 10000, 30));
    }
}

internal class MelonHandler : IModLoader
{
    public string ModPath =>
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    private event Action<float> _onUpdate;
    private event Action _onGUI;

    public void TriggerOnUpdate(float dt) => _onUpdate?.Invoke(dt);
    public void TriggerOnGUI() => _onGUI?.Invoke();

    public void Log(string msg) => MelonLogger.Msg(msg);
    public void Warning(string msg) => MelonLogger.Warning(msg);
    public void Error(string msg) => MelonLogger.Error(msg);

    public event Action<float> OnUpdate
    {
        add => _onUpdate += value;
        remove => _onUpdate -= value;
    }

    public event Action<bool> OnToggle { add { } remove { } }

    public event Action OnGUI
    {
        add => _onGUI += value;
        remove => _onGUI -= value;
    }

    public event Action OnSaveGUI { add { } remove { } }
}
