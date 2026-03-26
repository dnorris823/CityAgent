# TESTING
_Generated: 2026-03-26_

## Summary
There is no automated test suite in this codebase. No test files, no test runner config, and no CI/CD pipeline were found. Testing is entirely manual: build the DLL, deploy to CS2, and exercise the mod in-game. The one area with pure-function logic (`NarrativeMemorySystem.GenerateSlug`, `renderMarkdown`) is testable in isolation but has no tests written for it.

---

## Test Infrastructure

**Test runner:** None detected.

**Test files:** Zero. No `*.test.ts`, `*.spec.ts`, `*Test.cs`, or `*Tests.cs` files exist anywhere in the project.

**CI/CD:** No `.github/`, `.gitlab-ci.yml`, `azure-pipelines.yml`, or equivalent config found.

**Coverage tooling:** None.

---

## Manual Testing Approach

The project is validated entirely through build-and-deploy cycles:

**C# mod:**
```bash
# Close CS2 first (game locks the DLL)
cd src && dotnet build -c Release
# DLL deploys directly to:
# %AppData%/../LocalLow/Colossal Order/Cities Skylines II/Mods/CityAgent/
```

**UI:**
```bash
cd UI && npm run build
# Output deploys directly to the same mod folder alongside the DLL
```

Then launch CS2, load a city, and manually verify behavior.

---

## What Would Need Testing (If Tests Existed)

### Pure Functions — Highest Testability

**`NarrativeMemorySystem.GenerateSlug` (`src/Systems/NarrativeMemorySystem.cs:151`)**
- Marked `internal static` — directly callable from a test project in the same assembly or via `InternalsVisibleTo`.
- Input/output is pure string → string with no dependencies.
- Edge cases: empty string, Unicode characters, multiple spaces, leading/trailing hyphens, only special characters.

**`NarrativeMemorySystem.ChatHistoryToMarkdown` and `ParseChatSession` (`src/Systems/NarrativeMemorySystem.cs:544-602`)**
- Both are `public static` — directly testable.
- Round-trip property: `ParseChatSession(ChatHistoryToMarkdown(messages, ...))` should recover the original messages.
- Edge cases: empty history, messages with `*(with screenshot)*`, messages containing `### ` headers.

**`renderMarkdown` (`UI/src/utils/renderMarkdown.ts:216`)**
- Pure function, exported by name.
- Testable with Vitest or Jest in the UI project.
- Edge cases: nested markdown, emoji in headings, tables, fenced code blocks with special characters, XSS via `<script>` tags (HTML escaping).

**`NarrativeMemorySystem.UpdateFrontmatter` and `ParseEntryCount`**
- Private helpers but testable via `AppendToLog` integration.

### Integration Points — Harder to Test

**`CityToolRegistry.Dispatch`** — requires mock `ICityAgentTool` implementations. The interface is small and mockable.

**`ClaudeAPISystem.RunRequestAsync`** — requires mocking `HttpClient`. The static `s_Http` field makes this difficult without refactoring to inject the client.

**`CityAgentUISystem` bindings** — requires the CS2 runtime; not unit-testable.

**`CityDataSystem` ECS queries** — requires the Unity DOTS runtime; not unit-testable outside the game.

---

## If Adding Tests

### Recommended Setup for C# (xUnit)

Create a separate test project at `src.Tests/CityAgent.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../CityAgent.csproj" />
    <PackageReference Include="xunit" Version="2.x" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.x" />
  </ItemGroup>
</Project>
```

Add to `CityAgent.csproj`:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="CityAgent.Tests" />
</ItemGroup>
```

Example test structure:
```csharp
public class NarrativeMemorySystemTests
{
    [Theory]
    [InlineData("My City", "my-city")]
    [InlineData("  spaces  ", "spaces")]
    [InlineData("Special!@#Chars", "specialchars")]
    [InlineData("", "unnamed-city")]
    public void GenerateSlug_ProducesExpectedOutput(string input, string expected)
    {
        Assert.Equal(expected, NarrativeMemorySystem.GenerateSlug(input));
    }

    [Fact]
    public void ChatHistory_RoundTrip_PreservesMessages()
    {
        var messages = new List<(string, string, bool)>
        {
            ("user", "Hello city", false),
            ("assistant", "Here is my advice", false),
            ("user", "Take a look", true)
        };
        string markdown = NarrativeMemorySystem.ChatHistoryToMarkdown(messages, 1, "TestCity");
        var parsed = NarrativeMemorySystem.ParseChatSession(markdown);
        Assert.Equal(3, parsed.Count);
        Assert.Equal(("user", "Hello city", false), parsed[0]);
        Assert.Equal(("user", "Take a look", true), parsed[2]);
    }
}
```

### Recommended Setup for TypeScript (Vitest)

Add to `UI/package.json`:
```json
"devDependencies": {
  "vitest": "^1.x"
},
"scripts": {
  "test": "vitest run",
  "test:watch": "vitest"
}
```

Example test structure:
```typescript
// UI/src/utils/renderMarkdown.test.ts
import { describe, it, expect } from "vitest";
import { renderMarkdown } from "./renderMarkdown";

describe("renderMarkdown", () => {
  it("renders bold text", () => {
    expect(renderMarkdown("**hello**")).toBe("<p><strong>hello</strong></p>");
  });

  it("escapes HTML in code blocks", () => {
    expect(renderMarkdown("```\n<script>\n```")).toContain("&lt;script&gt;");
  });

  it("handles empty input", () => {
    expect(renderMarkdown("")).toBe("");
  });
});
```

---

## Gaps / Unknowns

- No test infrastructure of any kind exists. Every behavior change requires manual in-game verification.
- `ClaudeAPISystem` has a static `HttpClient` that cannot be injected or mocked without refactoring — this is the biggest barrier to unit-testing the API integration loop.
- `CityDataSystem` ECS queries and `CityAgentUISystem` bindings are inherently integration-level concerns that require the CS2 runtime to test. No mocking layer exists for CS2 ECS.
- The `renderMarkdown` function uses `var` throughout (instead of `const`/`let`) for Coherent GT compatibility — this has no impact on testability but would need to be noted if a linter is added.
- No property-based testing library is present. The slug generation and round-trip parsing logic would benefit from property-based tests.
