using Game;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CityAgent.Systems
{
    /// <summary>
    /// Manages per-city narrative memory files on disk.
    /// Each city gets a flat directory of markdown files the AI uses as long-term narrative memory.
    /// </summary>
    public partial class NarrativeMemorySystem : GameSystemBase
    {
        /// <summary>Core files that cannot be deleted by the delete_memory_file tool.</summary>
        private static readonly HashSet<string> CoreFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "_index.md",
            "characters.md",
            "districts.md",
            "city-plan.md",
            "narrative-log.md",
            "challenges.md",
            "milestones.md",
            "style-notes.md",
            "economy.md",
            "lore.md"
        };

        private string m_ModDir       = "";
        private string m_MemoryBase   = "";
        private string m_CitySlug     = "";
        private string m_CityName     = "";
        private string m_CityDir      = "";
        private bool   m_Initialized  = false;
        private int    m_SessionNumber = 0;

        // Configurable thresholds (read from Settings)
        private int m_MaxNarrativeLogEntries   = 50;
        private int m_MaxChatHistorySessions   = 20;

        public string CitySlug => m_CitySlug;
        public string CityName => m_CityName;
        public string CityDir  => m_CityDir;
        public bool   IsInitialized => m_Initialized;
        public int    SessionNumber => m_SessionNumber;

        protected override void OnCreate()
        {
            base.OnCreate();
            Mod.Log.Info($"{nameof(NarrativeMemorySystem)}.{nameof(OnCreate)}");
        }

        protected override void OnUpdate() { }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Mod.Log.Info($"{nameof(NarrativeMemorySystem)}.{nameof(OnDestroy)}");
        }

        // ── Initialization ─────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the mod directory (from TryGetExecutableAsset). Must be called before Initialize.
        /// </summary>
        public void SetModDir(string modDir)
        {
            m_ModDir = modDir;
            m_MemoryBase = Path.Combine(modDir, "memory");
        }

        /// <summary>
        /// Initializes the memory system for a city. Tries to resolve the city name
        /// from CS2, falls back to the provided fallback name.
        /// Creates directory scaffolding and template files if needed.
        /// </summary>
        public void Initialize(string? cityNameOverride = null)
        {
            if (string.IsNullOrEmpty(m_ModDir))
            {
                Mod.Log.Error("[NarrativeMemorySystem] ModDir not set. Call SetModDir first.");
                return;
            }

            // Read settings
            var setting = Mod.ActiveSetting;
            if (setting != null)
            {
                m_MaxNarrativeLogEntries = setting.MaxNarrativeLogEntries;
                m_MaxChatHistorySessions = setting.MaxChatHistorySessions;
            }

            // Resolve city name
            m_CityName = ResolveCityName(cityNameOverride);
            m_CitySlug = GenerateSlug(m_CityName);
            m_CityDir  = Path.Combine(m_MemoryBase, m_CitySlug);

            Mod.Log.Info($"[NarrativeMemorySystem] City: \"{m_CityName}\" slug: \"{m_CitySlug}\" dir: \"{m_CityDir}\"");

            // Ensure directory exists and scaffold template files
            EnsureDirectoryStructure();

            // Determine session number
            m_SessionNumber = DetermineNextSessionNumber();

            m_Initialized = true;
            Mod.Log.Info($"[NarrativeMemorySystem] Initialized. Session #{m_SessionNumber}");
        }

        public void StartNewSession()
        {
            if (!m_Initialized) return;
            m_SessionNumber++;
            Mod.Log.Info($"[NarrativeMemorySystem] New session started: #{m_SessionNumber}");
        }

        private string ResolveCityName(string? cityNameOverride)
        {
            if (!string.IsNullOrWhiteSpace(cityNameOverride))
                return cityNameOverride!.Trim();

            // Try CS2 city configuration system
            try
            {
                var cityConfigSystem = World.GetExistingSystemManaged<Game.City.CityConfigurationSystem>();
                if (cityConfigSystem != null)
                {
                    // CityConfigurationSystem may expose city name through its data
                    var cityName = cityConfigSystem.cityName;
                    if (!string.IsNullOrWhiteSpace(cityName))
                    {
                        Mod.Log.Info($"[NarrativeMemorySystem] Got city name from CityConfigurationSystem: {cityName}");
                        return cityName;
                    }
                }
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"[NarrativeMemorySystem] CityConfigurationSystem lookup failed: {ex.Message}");
            }

            return "Unnamed City";
        }

        // ── Slug Generation ─────────────────────────────────────────────────────────

        internal static string GenerateSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed-city";

            string slug = name.Trim().ToLowerInvariant();
            // Replace spaces/underscores with hyphens
            slug = Regex.Replace(slug, @"[\s_]+", "-");
            // Remove anything that isn't alphanumeric or hyphen
            slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
            // Collapse multiple hyphens
            slug = Regex.Replace(slug, @"-{2,}", "-");
            // Trim leading/trailing hyphens
            slug = slug.Trim('-');

            return string.IsNullOrEmpty(slug) ? "unnamed-city" : slug;
        }

        // ── Directory Scaffolding ───────────────────────────────────────────────────

        private void EnsureDirectoryStructure()
        {
            Directory.CreateDirectory(m_CityDir);
            Directory.CreateDirectory(Path.Combine(m_CityDir, "chat-history"));
            Directory.CreateDirectory(Path.Combine(m_CityDir, "archive"));

            // Write template files if they don't exist
            foreach (var kvp in GetTemplateFiles())
            {
                string filePath = Path.Combine(m_CityDir, kvp.Key);
                if (!File.Exists(filePath))
                {
                    File.WriteAllText(filePath, kvp.Value);
                    Mod.Log.Info($"[NarrativeMemorySystem] Created template: {kvp.Key}");
                }
            }
        }

        private Dictionary<string, string> GetTemplateFiles()
        {
            string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            return new Dictionary<string, string>
            {
                ["_index.md"] = $@"---
city_name: ""{EscapeYaml(m_CityName)}""
city_slug: ""{m_CitySlug}""
created: ""{now}""
last_updated: ""{now}""
play_sessions: 0
population_last_known: 0
---

# {m_CityName}

## City Identity
A new city awaiting its story.

## Current State
- Population: 0
- Main challenge: Getting started
- Current project: Initial development

## Active Narrative Threads
(none yet)

## Memory Files
- [characters.md](characters.md) - 0 characters
- [districts.md](districts.md) - 0 districts
- [city-plan.md](city-plan.md) - Initial plan
- [narrative-log.md](narrative-log.md) - 0 entries
- [challenges.md](challenges.md) - 0 active
- [milestones.md](milestones.md) - 0 milestones
- [style-notes.md](style-notes.md) - Default tone
- [economy.md](economy.md) - No data yet
- [lore.md](lore.md) - No lore yet
",
                ["characters.md"] = $@"---
last_updated: ""{now}""
character_count: 0
---

# Characters

(No characters created yet. The AI will add characters here as the city's story develops.)
",
                ["districts.md"] = $@"---
last_updated: ""{now}""
district_count: 0
---

# Districts

(No districts documented yet. The AI will add district narratives as the city grows.)
",
                ["city-plan.md"] = $@"---
last_updated: ""{now}""
plan_version: 1
---

# City Plan

## Vision
(The city's long-term vision will be developed through conversations with the advisor.)

## Current Priorities
1. Establish initial infrastructure
2. Begin residential development

## Completed Goals
(none yet)
",
                ["narrative-log.md"] = $@"---
last_updated: ""{now}""
entry_count: 0
---

# Narrative Log

(Chronological narrative events will be recorded here, newest first.)
",
                ["challenges.md"] = $@"---
last_updated: ""{now}""
active_count: 0
resolved_count: 0
---

# Challenges

## Active Challenges
(none yet)

## Resolved Challenges
(none yet)
",
                ["milestones.md"] = $@"---
last_updated: ""{now}""
milestone_count: 0
---

# Milestones

(Population milestones, achievements, and turning points will be recorded here.)
",
                ["style-notes.md"] = $@"---
last_updated: ""{now}""
---

# Style Notes

## Tone
Enthusiastic but practical, in the style of CityPlannerPlays. Mix narrative storytelling with actionable city-building advice.

## Vocabulary
- Use city planning terminology naturally
- Name infrastructure projects and districts
- Reference characters by name when relevant

## Running Themes
(Themes and running jokes will develop organically through play.)
",
                ["economy.md"] = $@"---
last_updated: ""{now}""
---

# Economy

(Economic narrative, budget history, trade patterns, and industry stories will be tracked here.)
",
                ["lore.md"] = $@"---
last_updated: ""{now}""
---

# Lore

(World-building, city mythology, local legends, and historical fiction will be recorded here.)
"
            };
        }

        private static string EscapeYaml(string value) => value.Replace("\"", "\\\"");

        // ── File I/O API ────────────────────────────────────────────────────────────

        /// <summary>Read a memory file by filename. Returns file contents or an error string.</summary>
        public string ReadFile(string filename)
        {
            if (!m_Initialized) return "[Error]: Memory system not initialized.";
            if (!ValidateFilename(filename, out string error)) return error;

            string path = Path.Combine(m_CityDir, filename);
            if (!File.Exists(path))
                return $"[Error]: File '{filename}' does not exist.";

            return File.ReadAllText(path);
        }

        /// <summary>Overwrite a memory file with new content.</summary>
        public string WriteFile(string filename, string content)
        {
            if (!m_Initialized) return "[Error]: Memory system not initialized.";
            if (!ValidateFilename(filename, out string error)) return error;

            string path = Path.Combine(m_CityDir, filename);
            if (!File.Exists(path))
                return $"[Error]: File '{filename}' does not exist. Use create_memory_file to create new files.";

            File.WriteAllText(path, content);
            Mod.Log.Info($"[NarrativeMemorySystem] Wrote {content.Length} chars to {filename}");
            return $"Successfully wrote {content.Length} characters to {filename}.";
        }

        /// <summary>Append an entry to narrative-log.md with a timestamp.</summary>
        public string AppendToLog(string entry)
        {
            if (!m_Initialized) return "[Error]: Memory system not initialized.";

            string path = Path.Combine(m_CityDir, "narrative-log.md");
            if (!File.Exists(path))
                return "[Error]: narrative-log.md does not exist.";

            string existing = File.ReadAllText(path);
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC", CultureInfo.InvariantCulture);

            // Parse entry count from frontmatter
            int entryCount = ParseEntryCount(existing) + 1;

            // Build new entry block
            string newEntry = $"\n---\n\n### [{timestamp}] Session #{m_SessionNumber}\n\n{entry.Trim()}\n";

            // Insert after the frontmatter+header, before existing entries
            int insertIndex = FindLogInsertionPoint(existing);
            string updated = existing.Substring(0, insertIndex) + newEntry + existing.Substring(insertIndex);

            // Update frontmatter entry count and last_updated
            updated = UpdateFrontmatter(updated, entryCount);

            File.WriteAllText(path, updated);
            Mod.Log.Info($"[NarrativeMemorySystem] Appended log entry #{entryCount}");

            // Check if archive rotation is needed
            if (entryCount > m_MaxNarrativeLogEntries)
                RotateNarrativeLog(path);

            return $"Appended entry to narrative-log.md (entry #{entryCount}).";
        }

        /// <summary>Create a new memory file. Fails if it already exists.</summary>
        public string CreateFile(string filename, string content)
        {
            if (!m_Initialized) return "[Error]: Memory system not initialized.";
            if (!ValidateFilename(filename, out string error)) return error;

            // Must be a .md file
            if (!filename.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                return "[Error]: Memory files must have a .md extension.";

            string path = Path.Combine(m_CityDir, filename);
            if (File.Exists(path))
                return $"[Error]: File '{filename}' already exists. Use write_memory_file to update it.";

            File.WriteAllText(path, content);
            Mod.Log.Info($"[NarrativeMemorySystem] Created new file: {filename}");
            return $"Successfully created {filename}.";
        }

        /// <summary>Delete a dynamically-created memory file. Core files cannot be deleted.</summary>
        public string DeleteFile(string filename)
        {
            if (!m_Initialized) return "[Error]: Memory system not initialized.";
            if (!ValidateFilename(filename, out string error)) return error;

            if (CoreFiles.Contains(filename))
                return $"[Error]: '{filename}' is a core memory file and cannot be deleted.";

            string path = Path.Combine(m_CityDir, filename);
            if (!File.Exists(path))
                return $"[Error]: File '{filename}' does not exist.";

            File.Delete(path);
            Mod.Log.Info($"[NarrativeMemorySystem] Deleted file: {filename}");
            return $"Successfully deleted {filename}.";
        }

        /// <summary>List all memory files with metadata.</summary>
        public string ListFiles()
        {
            if (!m_Initialized) return "[Error]: Memory system not initialized.";

            var files = new List<object>();
            foreach (var filePath in Directory.GetFiles(m_CityDir, "*.md"))
            {
                var info = new FileInfo(filePath);
                string name = info.Name;
                files.Add(new
                {
                    filename      = name,
                    size_bytes    = info.Length,
                    last_modified = info.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture),
                    is_core       = CoreFiles.Contains(name)
                });
            }

            return JsonConvert.SerializeObject(files, Formatting.Indented);
        }

        // ── Context Injection ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns the always-injected memory context: _index.md + style-notes.md contents.
        /// Prepended to the system prompt before every API call.
        /// </summary>
        public string GetAlwaysInjectedContext()
        {
            if (!m_Initialized) return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("\n\n--- CITY MEMORY (always loaded) ---\n");

            string indexPath = Path.Combine(m_CityDir, "_index.md");
            if (File.Exists(indexPath))
            {
                sb.AppendLine("### _index.md");
                sb.AppendLine(File.ReadAllText(indexPath));
                sb.AppendLine();
            }

            string stylePath = Path.Combine(m_CityDir, "style-notes.md");
            if (File.Exists(stylePath))
            {
                sb.AppendLine("### style-notes.md");
                sb.AppendLine(File.ReadAllText(stylePath));
                sb.AppendLine();
            }

            sb.AppendLine("--- END CITY MEMORY ---");
            sb.AppendLine();
            sb.AppendLine("### Memory Tool Instructions");
            sb.AppendLine("You have access to memory tools for persistent narrative storage:");
            sb.AppendLine("- Use `read_memory_file` to read specific memory files before referencing their content");
            sb.AppendLine("- Use `write_memory_file` to update existing memory files with new information");
            sb.AppendLine("- Use `append_narrative_log` to record important narrative events after substantive conversations");
            sb.AppendLine("- Use `create_memory_file` ONLY when the player explicitly asks you to create a new memory file");
            sb.AppendLine("- Use `delete_memory_file` to remove player-created files that are no longer needed");
            sb.AppendLine("- Use `list_memory_files` to see what memory files exist");
            sb.AppendLine();
            sb.AppendLine("If the city name is 'Unnamed City', ask the player what their city is called and update _index.md.");
            sb.AppendLine("After every substantive conversation, append a narrative summary to the log.");
            sb.AppendLine("When introducing characters, update characters.md. When challenges change, update challenges.md.");
            sb.AppendLine("Periodically update _index.md to reflect current city state.");

            return sb.ToString();
        }

        // ── Chat History Persistence ────────────────────────────────────────────────

        /// <summary>Save a chat session transcript to chat-history/session-NNN.md.</summary>
        public void SaveChatSession(string transcriptMarkdown)
        {
            if (!m_Initialized) return;

            string histDir = Path.Combine(m_CityDir, "chat-history");
            Directory.CreateDirectory(histDir);

            string filename = $"session-{m_SessionNumber:D3}.md";
            string path = Path.Combine(histDir, filename);
            File.WriteAllText(path, transcriptMarkdown);
            Mod.Log.Info($"[NarrativeMemorySystem] Saved chat session: {filename}");

            // Prune old sessions
            PruneChatHistory(histDir);
        }

        /// <summary>
        /// Fire-and-forget async wrapper for SaveChatSession.
        /// Moves file I/O off the main game thread (CORE-01, D-11, D-12).
        /// </summary>
        public Task SaveChatSessionAsync(string transcriptMarkdown)
        {
            return Task.Run(() =>
            {
                try
                {
                    SaveChatSession(transcriptMarkdown);
                }
                catch (Exception ex)
                {
                    Mod.Log.Error($"[NarrativeMemorySystem] SaveChatSessionAsync failed: {ex.Message}");
                }
            });
        }

        /// <summary>Load the most recent chat session transcript, if any.</summary>
        public string? LoadLatestChatSession()
        {
            if (!m_Initialized) return null;

            string histDir = Path.Combine(m_CityDir, "chat-history");
            if (!Directory.Exists(histDir)) return null;

            var files = Directory.GetFiles(histDir, "session-*.md")
                .OrderByDescending(f => f)
                .ToArray();

            if (files.Length == 0) return null;

            string latest = files[0];
            Mod.Log.Info($"[NarrativeMemorySystem] Loading latest chat session: {Path.GetFileName(latest)}");
            return File.ReadAllText(latest);
        }

        /// <summary>
        /// Converts the in-memory chat history to a markdown transcript for persistence.
        /// </summary>
        public static string ChatHistoryToMarkdown(IEnumerable<(string role, string content, bool hadImage)> messages, int sessionNumber, string cityName)
        {
            var sb = new System.Text.StringBuilder();
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC", CultureInfo.InvariantCulture);
            sb.AppendLine($"# Chat Session #{sessionNumber} — {cityName}");
            sb.AppendLine($"*Recorded: {timestamp}*");
            sb.AppendLine();

            foreach (var (role, content, hadImage) in messages)
            {
                string label = role == "user" ? "**Player**" : "**CityAgent**";
                sb.AppendLine($"### {label}");
                if (hadImage) sb.AppendLine("*(with screenshot)*");
                sb.AppendLine(content);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parses a chat session markdown file back into messages.
        /// Returns a list of (role, content, hadImage) tuples.
        /// </summary>
        public static List<(string role, string content, bool hadImage)> ParseChatSession(string markdown)
        {
            var messages = new List<(string role, string content, bool hadImage)>();
            if (string.IsNullOrWhiteSpace(markdown)) return messages;

            // Split on ### headers
            var sections = Regex.Split(markdown, @"(?=^### )", RegexOptions.Multiline);

            foreach (var section in sections)
            {
                if (!section.StartsWith("### ")) continue;

                string role;
                if (section.StartsWith("### **Player**"))
                    role = "user";
                else if (section.StartsWith("### **CityAgent**"))
                    role = "assistant";
                else
                    continue;

                bool hadImage = section.Contains("*(with screenshot)*");

                // Extract content after the header line
                var lines = section.Split('\n').Skip(1).ToList();
                // Remove the hadImage indicator line if present
                if (lines.Count > 0 && lines[0].Trim() == "*(with screenshot)*")
                    lines.RemoveAt(0);

                string content = string.Join("\n", lines).Trim();
                if (!string.IsNullOrEmpty(content))
                    messages.Add((role, content, hadImage));
            }

            return messages;
        }

        // ── Private Helpers ─────────────────────────────────────────────────────────

        private bool ValidateFilename(string filename, out string error)
        {
            error = "";

            if (string.IsNullOrWhiteSpace(filename))
            {
                error = "[Error]: Filename cannot be empty.";
                return false;
            }

            // Prevent path traversal
            if (filename.Contains("..") || filename.Contains('/') || filename.Contains('\\'))
            {
                error = "[Error]: Invalid filename. Path traversal is not allowed.";
                return false;
            }

            // Must not contain directory separator chars
            if (Path.GetFileName(filename) != filename)
            {
                error = "[Error]: Filename must not contain path separators.";
                return false;
            }

            return true;
        }

        private int DetermineNextSessionNumber()
        {
            string histDir = Path.Combine(m_CityDir, "chat-history");
            if (!Directory.Exists(histDir)) return 1;

            var files = Directory.GetFiles(histDir, "session-*.md");
            if (files.Length == 0) return 1;

            int maxSession = 0;
            foreach (var file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.StartsWith("session-") && int.TryParse(name.Substring(8), out int num))
                {
                    if (num > maxSession) maxSession = num;
                }
            }

            return maxSession + 1;
        }

        private int ParseEntryCount(string content)
        {
            var match = Regex.Match(content, @"entry_count:\s*(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        private int FindLogInsertionPoint(string content)
        {
            // Find the end of the "# Narrative Log" header section (after the first blank line after the header)
            var match = Regex.Match(content, @"# Narrative Log\s*\n");
            if (match.Success)
                return match.Index + match.Length;

            // Fallback: append at end
            return content.Length;
        }

        private string UpdateFrontmatter(string content, int entryCount)
        {
            string now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            content = Regex.Replace(content, @"entry_count:\s*\d+", $"entry_count: {entryCount}");
            content = Regex.Replace(content, @"last_updated:\s*""[^""]*""", $"last_updated: \"{now}\"");
            return content;
        }

        private void RotateNarrativeLog(string logPath)
        {
            try
            {
                string content = File.ReadAllText(logPath);

                // Split entries by the --- separator
                var entries = Regex.Split(content, @"\n---\n").ToList();
                if (entries.Count <= 1) return; // Only header, no entries

                // First element is frontmatter + header
                string header = entries[0];
                var logEntries = entries.Skip(1).ToList();

                if (logEntries.Count <= m_MaxNarrativeLogEntries) return;

                // Keep the newest entries, archive the rest
                var keep = logEntries.Take(m_MaxNarrativeLogEntries).ToList();
                var archive = logEntries.Skip(m_MaxNarrativeLogEntries).ToList();

                // Determine archive file number
                string archiveDir = Path.Combine(m_CityDir, "archive");
                Directory.CreateDirectory(archiveDir);
                int archiveNum = Directory.GetFiles(archiveDir, "narrative-log-*.md").Length + 1;
                string archivePath = Path.Combine(archiveDir, $"narrative-log-{archiveNum:D3}.md");

                // Write archived entries
                string archiveContent = $"# Narrative Log Archive #{archiveNum}\n\n" +
                    string.Join("\n---\n", archive);
                File.WriteAllText(archivePath, archiveContent);

                // Rewrite the main log with only kept entries
                string newContent = header + string.Join("\n---\n", keep.Select(e => "\n---\n" + e));
                newContent = Regex.Replace(newContent, @"entry_count:\s*\d+", $"entry_count: {keep.Count}");
                File.WriteAllText(logPath, newContent);

                Mod.Log.Info($"[NarrativeMemorySystem] Archived {archive.Count} log entries to narrative-log-{archiveNum:D3}.md");
            }
            catch (Exception ex)
            {
                Mod.Log.Error($"[NarrativeMemorySystem] Archive rotation failed: {ex.Message}");
            }
        }

        private void PruneChatHistory(string histDir)
        {
            try
            {
                var files = Directory.GetFiles(histDir, "session-*.md")
                    .OrderByDescending(f => f)
                    .ToArray();

                if (files.Length <= m_MaxChatHistorySessions) return;

                // Delete oldest sessions beyond the limit
                for (int i = m_MaxChatHistorySessions; i < files.Length; i++)
                {
                    File.Delete(files[i]);
                    Mod.Log.Info($"[NarrativeMemorySystem] Pruned old chat session: {Path.GetFileName(files[i])}");
                }
            }
            catch (Exception ex)
            {
                Mod.Log.Error($"[NarrativeMemorySystem] Chat history pruning failed: {ex.Message}");
            }
        }
    }
}
