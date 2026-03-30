/**
 * Formats a Unix timestamp (seconds) into a relative time string.
 * E.g. "just now", "5m ago", "2h ago", "3d ago", "1w ago"
 *
 * Uses var declarations throughout for Coherent GT compatibility
 * (matches renderMarkdown.ts convention).
 */
export function formatRelativeTime(unixSeconds: number): string {
  var nowSeconds = Math.floor(Date.now() / 1000);
  var diff = nowSeconds - unixSeconds;
  if (diff < 0) diff = 0;

  if (diff < 60) return "just now";

  var minutes = Math.floor(diff / 60);
  if (minutes < 60) return minutes + "m ago";

  var hours = Math.floor(diff / 3600);
  if (hours < 24) return hours + "h ago";

  var days = Math.floor(diff / 86400);
  if (days < 7) return days + "d ago";

  var weeks = Math.floor(diff / 604800);
  return weeks + "w ago";
}
