using System.Collections.Concurrent;
using Dalamud.Interface.Windowing;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace MinimapZoom;

public sealed unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle Lifecycle { get; private set; } = null!;
    [PluginService] internal static ISigScanner Scanner { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly ConcurrentQueue<Action> requests = new();
    private readonly MinimapAppearance appearance = new();
    private readonly Configuration configuration;
    private readonly NativeMinimap? native;
    private readonly string? compatibilityError;
    private volatile ViewState view = new(false, false, 0.5f, "Initialisation…", AppearanceSettings.Default);
    private volatile bool disposing;
    private readonly WindowSystem windowSystem = new("MinimapZoom");
    private readonly SettingsWindow window;
    private bool enabled;
    private AppearanceSettings appearanceSettings;
    private bool enableZoomOnStartup;
    private bool openWindowOnStartup;
    private bool applyPending;
    private bool configDirty;
    private DateTime saveAfter;
    private nint trackedAddon;
    private float restoreZoom;
    private float requestedZoom;
    private string? runtimeError;

    public Plugin()
    {
        var loaded = PluginInterface.GetPluginConfig() as Configuration;
        configuration = loaded ?? new Configuration();
        configuration.Version = 4;
        requestedZoom = ZoomPolicy.Extended(configuration.LastZoom);
        appearanceSettings = configuration.Appearance;
        enableZoomOnStartup = configuration.EnableZoomOnStartup;
        openWindowOnStartup = configuration.OpenWindowOnStartup;
        var actions = new SettingsActions(
            zoom => requests.Enqueue(() => EnableAt(zoom)),
            () => requests.Enqueue(() => { enableZoomOnStartup = false; DisableAndRestore(); RememberZoom(); }),
            () => requests.Enqueue(ResetAll), ChangeAppearance,
            value => requests.Enqueue(() => { enableZoomOnStartup = value; RememberZoom(); }),
            value => requests.Enqueue(() => { openWindowOnStartup = value; RememberZoom(); }));
        window = new SettingsWindow(() => view, actions, Diagnostic, () => compatibilityError == null);
        windowSystem.AddWindow(window);
        window.IsOpen = openWindowOnStartup;
        try
        {
            native = new NativeMinimap(Scanner, Interop, OnNativeZoom,
                () => enabled && !disposing && NativeMinimap.IsReady(CurrentAddon()) ? requestedZoom : 0.5f,
                exception => requests.Enqueue(() => HandleFailure(exception)));
            Log.Information($"Minimap Zoom {BuildIdentity.Version} (build {BuildIdentity.BuildId}) ready from {PluginInterface.AssemblyLocation.FullName}; client and native signatures validated. Settings: /minizoom.");
        }
        catch (Exception exception)
        {
            compatibilityError = exception.Message;
            window.IsOpen = true;
            Log.Error(exception, "Minimap Zoom remains inactive: native compatibility validation failed.");
        }

        Commands.AddHandler("/minizoom", new CommandInfo(OnCommand)
        {
            HelpMessage = "Réglages de la mini-carte. /minizoom on : appliquer le zoom mémorisé ; /minizoom off : tout restaurer.",
        });
        PluginInterface.UiBuilder.Draw += Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenWindow;
        PluginInterface.UiBuilder.OpenMainUi += OpenWindow;
        Framework.Update += OnUpdate;
        Lifecycle.RegisterListener(AddonEvent.PreFinalize, "_NaviMap", OnFinalize);
        Lifecycle.RegisterListener(AddonEvent.PreUpdate, "_NaviMap", OnAddonUpdate);
        Lifecycle.RegisterListener(AddonEvent.PostUpdate, "_NaviMap", OnAddonUpdate);
        Lifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "_NaviMap", OnAddonUpdate);
        Lifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_NaviMap", OnAddonUpdate);
        Lifecycle.RegisterListener(AddonEvent.PreDraw, "_NaviMap", OnAddonUpdate);
        if (native != null && enableZoomOnStartup) requests.Enqueue(() => EnableAt(requestedZoom));
    }

    private void OpenWindow() => window.IsOpen = true;

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "on": requests.Enqueue(() => EnableAt(requestedZoom)); break;
            case "off": requests.Enqueue(ResetAll); break;
            default: OpenWindow(); break;
        }
    }

    private void EnableAt(float zoom)
    {
        if (native == null) return;
        runtimeError = null;
        requestedZoom = ZoomPolicy.Extended(zoom);
        native.Enable();
        enabled = true;
        applyPending = true;
        RememberZoom();
    }

    private void OnUpdate(IFramework framework)
    {
        if (disposing) return;
        try
        {
            while (requests.TryDequeue(out var request)) request();
            if (native == null)
            {
                view = new(false, false, requestedZoom, compatibilityError ?? "Client incompatible", appearanceSettings);
                return;
            }
            var addon = CurrentAddon();
            var ready = ClientState.IsLoggedIn && NativeMinimap.IsReady(addon);
            if (enabled && ready)
            {
                if (trackedAddon != (nint)addon)
                {
                    // Addresses are used for identity only, never dereferenced after a lifecycle change.
                    trackedAddon = (nint)addon;
                    restoreZoom = ZoomPolicy.Native(addon->MarkerPositionScaling);
                    applyPending = true;
                }
                if (applyPending)
                {
                    native!.Apply(addon, requestedZoom, 1, true);
                    applyPending = false;
                    Log.Information($"Minimap Zoom applied: zoom={requestedZoom:0.00}, background={addon->MapImage->ScaleX:0.00}, restore={restoreZoom:0.00}.");
                }
                SanitizeSavedParameter(addon);
            }

            if (ready) appearance.Apply(addon, appearanceSettings);

            if (configDirty && DateTime.UtcNow >= saveAfter)
            {
                SaveConfiguration();
            }
            view = new(enabled, ready, ready ? addon->MarkerPositionScaling : requestedZoom,
                compatibilityError ?? runtimeError ?? (!ready ? "En attente de la mini-carte…" : enabled ? "Zoom personnalisé actif" : "Zoom du jeu"),
                appearanceSettings, enableZoomOnStartup, openWindowOnStartup);
        }
        catch (Exception exception)
        {
            HandleFailure(exception);
        }
    }

    private void OnAddonUpdate(AddonEvent type, AddonArgs args)
    {
        if (disposing || native == null) return;
        var addon = (AddonNaviMap*)args.Addon.Address;
        try
        {
            if (type is AddonEvent.PreUpdate or AddonEvent.PreRequestedUpdate) appearance.RestoreMarkerOverrides(addon);
            else if (ClientState.IsLoggedIn && NativeMinimap.IsReady(addon)) appearance.Apply(addon, appearanceSettings);
        }
        catch (Exception exception) { HandleFailure(exception); }
    }

    private void HandleFailure(Exception exception)
    {
        Log.Error(exception, "Minimap Zoom disabled following an update error.");
        runtimeError = exception.Message;
        window.IsOpen = true;
        while (requests.TryDequeue(out _)) { }
        try { ResetAll(); }
        catch (Exception restoreError) { Log.Error(restoreError, "Minimap Zoom restoration failed."); }
        view = new(false, false, requestedZoom, $"Désactivé : {exception.Message}", AppearanceSettings.Default);
    }

    private static AddonNaviMap* CurrentAddon() => (AddonNaviMap*)GameGui.GetAddonByName("_NaviMap").Address;

    private void OnNativeZoom(AddonNaviMap* addon, byte refresh)
    {
        if (!enabled || disposing || trackedAddon != (nint)addon || !NativeMinimap.IsReady(addon))
        {
            native!.Original(addon, refresh);
            return;
        }

        try
        {
            appearance.RestoreMarkerOverrides(addon);
            // Config reloads (refresh=0) keep the plugin preference; actual input uses the game's new value.
            var zoom = refresh == 0 ? requestedZoom : ZoomPolicy.Extended(addon->MarkerPositionScaling);
            native!.Apply(addon, zoom, refresh, true);
            if (requestedZoom != zoom)
            {
                requestedZoom = zoom;
                RememberZoom();
            }
        }
        catch (Exception exception)
        {
            // No managed exception may escape through a reverse native callback.
            enabled = false;
            runtimeError = exception.Message;
            Log.Error(exception, "Minimap Zoom native callback failed; falling back to native zoom.");
            native!.Original(addon, refresh);
            requests.Enqueue(ResetAll);
        }
    }

    private void RememberZoom()
    {
        configDirty = true;
        saveAfter = DateTime.UtcNow.AddMilliseconds(500);
    }

    private void SaveConfiguration()
    {
        configuration.LastZoom = requestedZoom;
        configuration.SquareMinimap = appearanceSettings.Square;
        configuration.HideMarkers = appearanceSettings.HideMarkers;
        configuration.HiddenCategories = appearanceSettings.HiddenCategories;
        configuration.MarkerScale = appearanceSettings.MarkerScale;
        configuration.PlayerScale = appearanceSettings.PlayerScale;
        configuration.HideFrame = appearanceSettings.HideFrame;
        configuration.HideSunMoon = appearanceSettings.HideSunMoon;
        configuration.HideWeather = appearanceSettings.HideWeather;
        configuration.HideButtons = appearanceSettings.HideButtons;
        configuration.FrameStyle = appearanceSettings.FrameStyle;
        configuration.FrameColor = appearanceSettings.FrameColor;
        configuration.EnableZoomOnStartup = enableZoomOnStartup;
        configuration.OpenWindowOnStartup = openWindowOnStartup;
        PluginInterface.SavePluginConfig(configuration);
        configDirty = false;
    }

    private void ResetAll()
    {
        appearanceSettings = AppearanceSettings.Default;
        enableZoomOnStartup = false;
        RememberZoom();
        DisableAndRestore();
    }

    private static void SanitizeSavedParameter(AddonNaviMap* addon)
    {
        // Native input encodes zoom in Param after the hook returns. Keep that parameter native-valid.
        // This does not force a save or change the north-lock bits.
        if ((addon->Param & 0xFFFF) < 50 || (addon->Param & 0xFFFF) > 200)
            addon->Param = ZoomPolicy.NativeParameter(addon->Param, addon->MarkerPositionScaling);
    }

    private void Restore(AddonNaviMap* addon, byte refresh)
    {
        if (native == null || addon == null || trackedAddon != (nint)addon) return;
        native.Apply(addon, restoreZoom, refresh, false);
        addon->Param = ZoomPolicy.NativeParameter(addon->Param, restoreZoom);
        Log.Information($"Minimap Zoom restored native zoom {restoreZoom:0.00}.");
        trackedAddon = 0;
    }

    private void DisableAndRestore()
    {
        enabled = false;
        applyPending = false;
        try
        {
            if (native != null && !Framework.IsFrameworkUnloading)
            {
                var addon = CurrentAddon();
                if (NativeMinimap.IsReady(addon))
                {
                    appearance.Restore(addon);
                    Restore(addon, 1);
                }
            }
        }
        finally
        {
            native?.Disable();
            trackedAddon = 0;
        }
    }

    private void OnFinalize(AddonEvent type, AddonArgs args)
    {
        var addon = (AddonNaviMap*)args.Addon.Address;
        try
        {
            appearance.Restore(addon);
            // PreFinalize still owns the nodes, but a full marker refresh is unnecessary during teardown.
            Restore(addon, 0);
        }
        catch (Exception exception)
        {
            enabled = false;
            runtimeError = exception.Message;
            Log.Error(exception, "Minimap Zoom restoration during addon teardown failed.");
        }
        finally
        {
            trackedAddon = 0;
            applyPending = enabled;
        }
    }

    private void Draw()
    {
        if (!disposing) windowSystem.Draw();
    }

    private void ChangeAppearance(Func<AppearanceSettings, AppearanceSettings> change) => requests.Enqueue(() =>
    {
        appearanceSettings = change(appearanceSettings).Normalize();
        RememberZoom();
    });

    private string Diagnostic() =>
        $"Client : {NativeContracts.GameVersion}\nMini-carte disponible : {(view.Ready ? "oui" : "non")}\n" +
        $"Coefficient : {view.Zoom:0.00}\nVersion chargée : {BuildIdentity.Version} · build {BuildIdentity.BuildId}\n" +
        $"DLL : {PluginInterface.AssemblyLocation.FullName}\nPortée étendue : {native?.RangeExpansionCount ?? 0} mises à jour";
    public void Dispose()
    {
        if (disposing) return;
        disposing = true;
        PluginInterface.UiBuilder.Draw -= Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenWindow;
        PluginInterface.UiBuilder.OpenMainUi -= OpenWindow;
        Commands.RemoveHandler("/minizoom");
        windowSystem.RemoveAllWindows();
        window.Dispose();

        void Cleanup()
        {
            Framework.Update -= OnUpdate;
            Lifecycle.UnregisterListener(AddonEvent.PreFinalize, "_NaviMap", OnFinalize);
            Lifecycle.UnregisterListener(AddonEvent.PreUpdate, "_NaviMap", OnAddonUpdate);
            Lifecycle.UnregisterListener(AddonEvent.PostUpdate, "_NaviMap", OnAddonUpdate);
            Lifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, "_NaviMap", OnAddonUpdate);
            Lifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, "_NaviMap", OnAddonUpdate);
            Lifecycle.UnregisterListener(AddonEvent.PreDraw, "_NaviMap", OnAddonUpdate);
            try { DisableAndRestore(); }
            finally { native?.Dispose(); }
            if (configDirty)
            {
                SaveConfiguration();
            }
        }

        if (Framework.IsInFrameworkUpdateThread || Framework.IsFrameworkUnloading) Cleanup();
        else Framework.RunOnFrameworkThread(Cleanup).GetAwaiter().GetResult();
    }

}
