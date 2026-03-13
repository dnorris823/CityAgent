/**
 * CityAgent UI entry point.
 *
 * CS2 loads this as an ES module ({ModName}.mjs alongside the DLL).
 * The game calls the default export with its module registry, then auto-loads
 * the companion CSS file because we export hasCSS = true.
 */
import React from "react";
import { ModRegistrar } from "cs2/modding";
import { CityAgentPanel } from "./components/CityAgentPanel";
import "./style.css";

// Tell CS2 to auto-load CityAgent.css alongside this module
export const hasCSS = true;

const register: ModRegistrar = (moduleRegistry) => {
  // "Game.MainScreen" renders our component as an overlay on the main game view
  moduleRegistry.append("Game.MainScreen", CityAgentPanel);
};

export default register;
