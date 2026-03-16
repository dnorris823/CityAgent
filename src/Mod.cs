using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.UI;
using Game;
using Game.Modding;
using Game.SceneFlow;
using System.IO;

namespace CityAgent
{
    /// <summary>
    /// CityAgent mod entry point. Implements IMod so CS2 can discover and load it.
    /// Kept intentionally thin — just registers settings and schedules systems.
    /// </summary>
    public class Mod : IMod
    {
        public static readonly ILog Log = LogManager
            .GetLogger($"{nameof(CityAgent)}.{nameof(Mod)}")
            .SetShowsErrorsInUI(false);

        private Setting? m_Setting;

        public static Setting? ActiveSetting { get; private set; }

        // CS2 uses "ui-mods" as the shared coui:// host for all mod UI assets.
        // Adding our mod root to it makes coui://ui-mods/CityAgent.mjs resolve correctly.
        internal const string UIHostName = "ui-mods";

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info(nameof(OnLoad));

            string modDir = "";
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Log.Info($"CityAgent mod asset path: {asset.path}");

                // Register our UI folder with the game's coui:// resource handler.
                // This makes coui://cityAgent/ serve files from {ModFolder}/UI/.
                // The game's main React app listens for OnHostLocationAdded and
                // auto-imports coui://{hostName}/index.js to mount the mod UI.
                // Register the mod root folder (not a UI subfolder) under "ui-mods".
                // This makes coui://ui-mods/CityAgent.mjs serve our UI module.
                modDir = Path.GetDirectoryName(asset.path)!;
                UIManager.defaultUISystem.AddHostLocation(UIHostName, modDir, false, 0);
                Log.Info($"CityAgent UI registered: coui://{UIHostName}/ -> {modDir}");
            }

            // Register mod settings (shows up in the game's options menu)
            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));
            AssetDatabase.global.LoadSettings(nameof(CityAgent), m_Setting, new Setting(this));
            ActiveSetting = m_Setting;

            // Schedule our UI system to run during the UI update phase
            updateSystem.UpdateAt<Systems.CityAgentUISystem>(SystemUpdatePhase.UIUpdate);

            // Schedule ECS data reader and API system during game simulation
            updateSystem.UpdateAt<Systems.CityDataSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.ClaudeAPISystem>(SystemUpdatePhase.GameSimulation);

            // Schedule narrative memory system and pass the mod directory
            updateSystem.UpdateAt<Systems.NarrativeMemorySystem>(SystemUpdatePhase.GameSimulation);
            if (!string.IsNullOrEmpty(modDir))
            {
                var memorySystem = updateSystem.World.GetOrCreateSystemManaged<Systems.NarrativeMemorySystem>();
                memorySystem.SetModDir(modDir);
            }
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));

            UIManager.defaultUISystem.RemoveHostLocation(UIHostName);

            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
            ActiveSetting = null;
        }
    }
}
