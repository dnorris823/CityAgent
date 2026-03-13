using Colossal.UI;
using Colossal.UI.Binding;
using Game;
using Game.UI;

namespace CityAgent.Systems
{
    /// <summary>
    /// Phase 1: UI bridge system.
    ///
    /// Exposes two bindings to the React UI:
    ///   - panelVisible (bool)  — whether the CityAgent panel is open
    ///   - togglePanel (trigger) — flips panelVisible
    ///
    /// Also bootstraps the UI module by calling ExecuteScript to import CityAgent.mjs,
    /// since the game only auto-imports .mjs modules for Paradox Mods subscribed mods,
    /// not local development mods.
    /// </summary>
    public partial class CityAgentUISystem : UISystemBase
    {
        private const string BindingGroup = "cityAgent";

        private ValueBinding<bool> m_PanelVisible = null!;

        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.Log.Info($"{nameof(CityAgentUISystem)}.{nameof(OnCreate)}");

            m_PanelVisible = new ValueBinding<bool>(BindingGroup, "panelVisible", false);
            AddBinding(m_PanelVisible);
            AddBinding(new TriggerBinding(BindingGroup, "togglePanel", TogglePanel));

            Mod.Log.Info("CityAgent UI bindings registered.");

            // Explicitly import our UI module.
            // The game doesn't auto-import local mod .mjs files the way it does for
            // subscribed mods. We trigger the import here and pass window["cs2/modding"]
            // as the module registry (it's exposed as a window global like the other CS2 APIs).
            LoadUI();
        }

        private void LoadUI()
        {
            // Phase 1: inject a button + panel directly into the DOM.
            // The game's module registry (needed for React component injection) is only
            // passed to subscribed mods automatically. For local dev mods we inject raw HTML,
            // which is sufficient to validate the full toolchain for Phase 1.
            // C# bindings (panelVisible, togglePanel) still work via engine.trigger.
            const string script = @"
(function() {
    if (document.getElementById('city-agent-btn')) return; // already injected

    var btn = document.createElement('button');
    btn.id = 'city-agent-btn';
    btn.textContent = '\uD83C\uDFD9 CityAgent';
    btn.style.cssText = [
        'position:fixed',
        'bottom:80px',
        'right:20px',
        'z-index:10000',
        'background:#1a2a3a',
        'color:#e0e8f0',
        'border:1px solid #3a5a7a',
        'border-radius:6px',
        'padding:8px 16px',
        'font-size:14px',
        'cursor:pointer',
        'font-family:inherit'
    ].join(';');

    var panel = document.createElement('div');
    panel.id = 'city-agent-panel';
    panel.style.cssText = [
        'display:none',
        'position:fixed',
        'bottom:130px',
        'right:20px',
        'width:360px',
        'background:#0f1e2e',
        'border:1px solid #3a5a7a',
        'border-radius:8px',
        'z-index:9999',
        'color:#e0e8f0',
        'font-family:inherit',
        'overflow:hidden'
    ].join(';');
    panel.innerHTML =
        '<div style=""padding:10px 14px;background:#162436;border-bottom:1px solid #3a5a7a;font-size:14px;font-weight:600;display:flex;justify-content:space-between;align-items:center"">' +
            '<span>CityAgent AI Advisor</span>' +
            '<button onclick=""document.getElementById(\'city-agent-panel\').style.display=\'none\';engine.trigger(\'cityAgent\',\'togglePanel\')"" style=""background:none;border:none;color:#7a9ab5;font-size:18px;cursor:pointer;line-height:1"">&#x2715;</button>' +
        '</div>' +
        '<div style=""padding:14px;font-size:13px;line-height:1.5;color:#5a7a95;font-style:italic;text-align:center;margin-top:40px"">' +
            'CityAgent is ready.<br><br>City data and AI chat coming soon.' +
        '</div>';

    btn.onclick = function() {
        var p = document.getElementById('city-agent-panel');
        var visible = p.style.display !== 'none';
        p.style.display = visible ? 'none' : 'block';
        engine.trigger('cityAgent', 'togglePanel');
    };

    document.body.appendChild(panel);
    document.body.appendChild(btn);
    console.error('[CityAgent] UI injected successfully.');
})();
";
            try
            {
                UIManager.defaultUIView.View.ExecuteScript(script);
                Mod.Log.Info("CityAgent UI module import triggered.");
            }
            catch (System.Exception ex)
            {
                Mod.Log.Error($"CityAgent failed to trigger UI import: {ex.Message}");
            }
        }

        protected override void OnUpdate()
        {
            // Phase 1: nothing to poll each frame.
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mod.Log.Info($"{nameof(CityAgentUISystem)}.{nameof(OnDestroy)}");
        }

        private void TogglePanel()
        {
            m_PanelVisible.Update(!m_PanelVisible.value);
            Mod.Log.Info($"Panel toggled — now {(m_PanelVisible.value ? "open" : "closed")}");
        }
    }
}
