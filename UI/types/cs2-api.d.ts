/**
 * Type stubs for CS2's runtime-injected cs2/api module.
 * These are provided by the game (Coherent GT) at runtime — never bundled.
 * Expand as you discover more of the API surface.
 */
declare module "cs2/api" {
  /** Bind to a C# ValueBinding<T>. Returns an observable. */
  export function bindValue<T>(group: string, name: string): { value: T };

  /** Subscribe to a bound value. Returns an unsubscribe function. */
  export function useValue<T>(binding: { value: T }): T;

  /** Call a C# TriggerBinding. */
  export function trigger(group: string, name: string, ...args: unknown[]): void;
}
