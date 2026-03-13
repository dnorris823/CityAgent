/**
 * Type stubs for CS2's runtime-injected cs2/modding module.
 */
declare module "cs2/modding" {
  import React from "react";

  export interface ModuleRegistry {
    /** Append a component to an existing game UI slot. */
    append(id: string, component: React.ComponentType<any>): void;
    /** Replace/extend an existing game UI component. */
    extend(id: string, component: React.ComponentType<any>): void;
  }

  /** The shape of your mod's default export. */
  export type ModRegistrar = (moduleRegistry: ModuleRegistry) => void;
}
