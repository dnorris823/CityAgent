import React from "react";
import { bindValue, useValue, trigger } from "cs2/api";

// Bind to the C# ValueBinding<bool>("cityAgent", "panelVisible")
const panelVisible$ = bindValue<boolean>("cityAgent", "panelVisible");

/**
 * Phase 1: CityAgent panel component.
 *
 * - Renders a toggle button (always visible)
 * - When panelVisible is true, renders the panel overlay
 * - Button/close calls the C# TriggerBinding("cityAgent", "togglePanel")
 *
 * The C# side (CityAgentUISystem) owns the state — React only reflects it.
 */
export const CityAgentPanel: React.FC = () => {
  const panelVisible = useValue(panelVisible$);

  const handleToggle = () => trigger("cityAgent", "togglePanel");

  return (
    <>
      {/* Toggle button — always shown so the player can open the panel */}
      <button className="city-agent-toggle-btn" onClick={handleToggle}>
        🏙 CityAgent
      </button>

      {/* Panel — only rendered when C# says it's visible */}
      {panelVisible && (
        <div className="city-agent-panel">
          <div className="city-agent-panel__header">
            <span>CityAgent AI Advisor</span>
            <button className="city-agent-panel__close" onClick={handleToggle}>
              ✕
            </button>
          </div>

          <div className="city-agent-panel__body">
            {/* Phase 1 placeholder — replaced with chat UI in later phases */}
            <p className="city-agent-panel__placeholder">
              CityAgent is ready.
              <br />
              <br />
              City data and AI chat coming soon.
            </p>
          </div>
        </div>
      )}
    </>
  );
};
