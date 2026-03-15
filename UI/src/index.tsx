/**
 * CityAgent UI entry point.
 *
 * CS2 loads this as an ES module ({ModName}.mjs alongside the DLL).
 * The game calls the default export with its module registry.
 *
 * moduleRegistry.append("Game.MainScreen", ...) does NOT render in CS2 v1.5.5,
 * so we mount the React component directly into the DOM via ReactDOM.
 */
import React from "react";
import { ModRegistrar } from "cs2/modding";
import { CityAgentPanel } from "./components/CityAgentPanel";
import "./style.css";

// Tell CS2 to auto-load CityAgent.css alongside this module
export const hasCSS = true;

const register: ModRegistrar = (moduleRegistry) => {
  // Create a container for our React component
  const container = document.createElement("div");
  container.id = "city-agent-root";
  document.body.appendChild(container);

  try {
    // Access ReactDOM from the window global (provided by CS2 runtime)
    const RD: any = (window as any).ReactDOM;
    if (!RD) throw new Error("ReactDOM not available");

    if (typeof RD.createRoot === "function") {
      RD.createRoot(container).render(React.createElement(CityAgentPanel));
    } else {
      RD.render(React.createElement(CityAgentPanel), container);
    }
  } catch (e: any) {
    // Show error in-game if React rendering fails
    container.style.cssText =
      "position:fixed;bottom:80px;right:20px;z-index:99999;" +
      "background:#c00;color:#fff;padding:8px 16px;border-radius:6px;" +
      "font-size:13px;font-family:sans-serif;";
    container.textContent = "CityAgent: " + (e?.message || "render failed");
  }
};

export default register;
