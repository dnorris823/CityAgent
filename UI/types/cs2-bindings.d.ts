/**
 * Type stubs for CS2's runtime-injected cs2/bindings module.
 * Expand as needed.
 */
declare module "cs2/bindings" {
  export function bindValue<T>(group: string, name: string): { value: T };
}
