/**
 * Lightweight markdown-to-HTML renderer for Coherent GT (COHTML).
 *
 * Coherent GT ships an older Chromium that lacks Array.at(), unicode property
 * escapes (\p{P}), and other modern JS features — so we can't use `marked` or
 * similar libraries.  This hand-rolled renderer covers the subset of markdown
 * that Claude responses actually use: headings, bold, italic, strikethrough,
 * inline code, fenced code blocks, blockquotes, lists, links, hrs, and tables.
 *
 * Emoji characters are replaced with Twemoji SVG <img> tags since COHTML has
 * no emoji font support.
 */

var TWEMOJI_BASE = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/svg/";

/** Replace Unicode typography with ASCII equivalents that COHTML can render. */
function normalizeUnicode(text: string): string {
  return text
    .replace(/\u2013/g, "-")      // en dash
    .replace(/\u2014/g, "--")     // em dash
    .replace(/\u2015/g, "--")     // horizontal bar
    .replace(/\u2018/g, "'")      // left single quote
    .replace(/\u2019/g, "'")      // right single quote
    .replace(/\u201C/g, '"')      // left double quote
    .replace(/\u201D/g, '"')      // right double quote
    .replace(/\u2026/g, "...")    // ellipsis
    .replace(/\u00A0/g, " ")     // non-breaking space
    .replace(/\u2010/g, "-")      // hyphen
    .replace(/\u2011/g, "-")      // non-breaking hyphen
    .replace(/\u2012/g, "-")      // figure dash
    .replace(/\u2022/g, "-")      // bullet
    .replace(/\u2023/g, "-")      // triangular bullet
    .replace(/\u2043/g, "-")      // hyphen bullet
    .replace(/\u00B7/g, "-")      // middle dot
    .replace(/\u2024/g, ".")      // one dot leader
    .replace(/\u2025/g, "..")     // two dot leader
    .replace(/\u2032/g, "'")      // prime
    .replace(/\u2033/g, '"')      // double prime
    .replace(/\u2039/g, "<")      // single left angle quote
    .replace(/\u203A/g, ">")      // single right angle quote
    .replace(/\u00AB/g, '"')      // left guillemet
    .replace(/\u00BB/g, '"');     // right guillemet
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

/** Build Twemoji filename: strip trailing fe0f (Twemoji convention). */
function twemojiFilename(cps: number[]): string {
  // Remove trailing FE0F — Twemoji omits it for most single emoji.
  // Keep FE0F in the middle of compound sequences (before ZWJ).
  while (cps.length > 1 && cps[cps.length - 1] === 0xFE0F) {
    cps = cps.slice(0, cps.length - 1);
  }
  var parts: string[] = [];
  for (var k = 0; k < cps.length; k++) {
    parts.push(cps[k].toString(16));
  }
  return parts.join("-");
}

/** Build an emoji <span> with background-image. Failed loads show nothing. */
function emojiSpan(cps: number[]): string {
  var filename = twemojiFilename(cps);
  return '<span class="ca-emoji" style="background-image:url(' + TWEMOJI_BASE + filename + '.svg)"></span>';
}

/** Check if a BMP code point is an emoji that Twemoji covers. */
function isBmpEmoji(code: number): boolean {
  return (
    code === 0x00A9 || code === 0x00AE || // ©®
    code === 0x2122 || // ™
    (code >= 0x2139 && code <= 0x2199) ||
    code === 0x21A9 || code === 0x21AA ||
    (code >= 0x231A && code <= 0x23FF) ||
    (code >= 0x25AA && code <= 0x25FE) ||
    (code >= 0x2600 && code <= 0x27BF) ||
    (code >= 0x2934 && code <= 0x2935) ||
    (code >= 0x2B05 && code <= 0x2B55) ||
    (code >= 0x3030 && code <= 0x303D) ||
    code === 0x3297 || code === 0x3299
  );
}

/** Replace emoji characters with Twemoji <img> tags. */
function replaceEmoji(text: string): string {
  var result = "";
  var i = 0;
  while (i < text.length) {
    var code = text.charCodeAt(i);

    // Surrogate pair — supplementary plane (most emoji live here)
    if (code >= 0xD800 && code <= 0xDBFF && i + 1 < text.length) {
      var low = text.charCodeAt(i + 1);
      if (low >= 0xDC00 && low <= 0xDFFF) {
        var cp = ((code - 0xD800) * 0x400) + (low - 0xDC00) + 0x10000;

        // Treat all supplementary-plane surrogate pairs as potential emoji.
        // Non-emoji will fail to load from Twemoji and get hidden by onerror.
        if (cp >= 0x1F000) {
          var cps: number[] = [cp];
          var j = i + 2;

          // Consume modifiers: variation selectors, ZWJ sequences, skin tones
          while (j < text.length) {
            var nc = text.charCodeAt(j);

            // Variation selector U+FE0F
            if (nc === 0xFE0F) { cps.push(0xFE0F); j++; continue; }

            // Zero-width joiner U+200D
            if (nc === 0x200D && j + 1 < text.length) {
              cps.push(0x200D);
              j++;
              var zc = text.charCodeAt(j);
              // Next is surrogate pair
              if (zc >= 0xD800 && zc <= 0xDBFF && j + 1 < text.length) {
                var zl = text.charCodeAt(j + 1);
                if (zl >= 0xDC00 && zl <= 0xDFFF) {
                  cps.push(((zc - 0xD800) * 0x400) + (zl - 0xDC00) + 0x10000);
                  j += 2;
                  continue;
                }
              }
              // Next is BMP char (e.g. ♂ U+2642)
              if (zc >= 0x2000 && zc <= 0x3300) {
                cps.push(zc);
                j++;
                continue;
              }
              continue;
            }

            // Skin tone modifier U+1F3FB–U+1F3FF (surrogate pair)
            if (nc >= 0xD800 && nc <= 0xDBFF && j + 1 < text.length) {
              var sl = text.charCodeAt(j + 1);
              if (sl >= 0xDC00 && sl <= 0xDFFF) {
                var scp = ((nc - 0xD800) * 0x400) + (sl - 0xDC00) + 0x10000;
                if (scp >= 0x1F3FB && scp <= 0x1F3FF) {
                  cps.push(scp);
                  j += 2;
                  continue;
                }
              }
            }
            break;
          }

          result += emojiSpan(cps);
          i = j;
          continue;
        }

        // Non-emoji surrogate pair — strip (COHTML can't render these)
        i += 2;
        continue;
      }
    }

    // Standalone variation selector — skip
    if (code === 0xFE0F || code === 0xFE0E) { i++; continue; }

    // BMP emoji
    if (isBmpEmoji(code)) {
      var bmpCps: number[] = [code];
      var next = i + 1;
      if (next < text.length && text.charCodeAt(next) === 0xFE0F) {
        bmpCps.push(0xFE0F);
        next++;
      }
      result += emojiSpan(bmpCps);
      i = next;
      continue;
    }

    // Strip other non-renderable Unicode (COHTML lacks glyphs for these)
    if (code >= 0x200B && code <= 0x200F) { i++; continue; } // zero-width chars
    if (code >= 0x2028 && code <= 0x202F) { i++; continue; } // line/paragraph separators
    if (code === 0x20E3) { i++; continue; } // combining enclosing keycap
    if (code >= 0xFFF0 && code <= 0xFFFF) { i++; continue; } // specials

    result += text.charAt(i);
    i++;
  }
  return result;
}

function inlinePass(line: string): string {
  // Inline code (backtick) — must come first so code content isn't processed
  line = line.replace(/`([^`]+)`/g, function (_m, code) {
    return "<code>" + escapeHtml(code) + "</code>";
  });
  // Bold + italic (*** or ___)
  line = line.replace(/\*\*\*(.+?)\*\*\*/g, "<strong><em>$1</em></strong>");
  line = line.replace(/___(.+?)___/g, "<strong><em>$1</em></strong>");
  // Bold (** or __)
  line = line.replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
  line = line.replace(/__(.+?)__/g, "<strong>$1</strong>");
  // Italic (* or _)
  line = line.replace(/\*(.+?)\*/g, "<em>$1</em>");
  line = line.replace(/(^|[\s.,!?;:'"([{])_([^_]+?)_([\s.,!?;:'")\]}]|$)/g,
    function(_m, pre, content, post) {
      return pre + "<em>" + content + "</em>" + post;
    }
  );
  // Strikethrough
  line = line.replace(/~~(.+?)~~/g, "<del>$1</del>");
  // Links [text](url)
  line = line.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2">$1</a>');
  // Emoji — run last so it doesn't interfere with other patterns
  line = replaceEmoji(line);
  return line;
}

export function renderMarkdown(markdown: string): string {
  markdown = normalizeUnicode(markdown);
  var lines = markdown.split("\n");
  var out: string[] = [];
  var i = 0;

  while (i < lines.length) {
    var line = lines[i];

    // Fenced code block
    var fenceMatch = line.match(/^```(\w*)/);
    if (fenceMatch) {
      var codeLines: string[] = [];
      i++;
      while (i < lines.length && !lines[i].match(/^```\s*$/)) {
        codeLines.push(escapeHtml(lines[i]));
        i++;
      }
      i++; // skip closing ```
      var lang = fenceMatch[1];
      if (lang) {
        out.push('<span class="ca-code-lang">' + escapeHtml(lang) + "</span>");
      }
      out.push("<pre><code>" + codeLines.join("\n") + "</code></pre>");
      continue;
    }

    // Blank line
    if (line.trim() === "") {
      i++;
      continue;
    }

    // Horizontal rule
    if (/^(\*{3,}|-{3,}|_{3,})\s*$/.test(line)) {
      out.push("<hr>");
      i++;
      continue;
    }

    // Headings
    var headingMatch = line.match(/^(#{1,6})\s+(.+?)(?:\s+#+\s*)?$/);
    if (headingMatch) {
      var level = headingMatch[1].length;
      out.push("<h" + level + ">" + inlinePass(headingMatch[2]) + "</h" + level + ">");
      i++;
      continue;
    }

    // Blockquote (collect consecutive > lines)
    if (/^>\s?/.test(line)) {
      var bqLines: string[] = [];
      while (i < lines.length && /^>\s?/.test(lines[i])) {
        bqLines.push(lines[i].replace(/^>\s?/, ""));
        i++;
      }
      out.push("<blockquote>" + renderMarkdown(bqLines.join("\n")) + "</blockquote>");
      continue;
    }

    // Unordered list
    if (/^[\s]*[-*+]\s/.test(line)) {
      // Determine baseline indent from the first item in this list
      var baseIndentMatch = lines[i].match(/^(\s*)/);
      var baseIndent = baseIndentMatch ? baseIndentMatch[1].length : 0;
      out.push("<ul>");
      while (i < lines.length && /^[\s]*[-*+]\s/.test(lines[i])) {
        var itemIndentMatch = lines[i].match(/^(\s*)/);
        var itemIndent = itemIndentMatch ? itemIndentMatch[1].length : 0;
        var isSub = itemIndent > baseIndent;
        var itemText = inlinePass(lines[i].replace(/^[\s]*[-*+]\s+/, ""));
        if (isSub) {
          // Sub-item: wrap in nested <ul>
          out.push("<li><ul><li>" + itemText + "</li></ul></li>");
        } else {
          // Check if next line is a sub-item (to embed the sub-list inside this <li>)
          var nextIdx = i + 1;
          var hasSubNext = nextIdx < lines.length && /^[\s]*[-*+]\s/.test(lines[nextIdx]);
          var nextIndentMatch = hasSubNext ? lines[nextIdx].match(/^(\s*)/) : null;
          var nextIndent = nextIndentMatch ? nextIndentMatch[1].length : 0;
          if (hasSubNext && nextIndent > baseIndent) {
            // Collect the sub-items that follow this top-level item
            i++;
            var subItems = "";
            while (i < lines.length && /^[\s]*[-*+]\s/.test(lines[i])) {
              var subIndentMatch = lines[i].match(/^(\s*)/);
              var subIndent = subIndentMatch ? subIndentMatch[1].length : 0;
              if (subIndent <= baseIndent) break; // back to top level
              var subText = inlinePass(lines[i].replace(/^[\s]*[-*+]\s+/, ""));
              subItems += "<li>" + subText + "</li>";
              i++;
            }
            out.push("<li>" + itemText + "<ul>" + subItems + "</ul></li>");
            continue; // i already advanced
          } else {
            out.push("<li>" + itemText + "</li>");
          }
        }
        i++;
      }
      out.push("</ul>");
      continue;
    }

    // Ordered list
    if (/^[\s]*\d+[.)]\s/.test(line)) {
      out.push("<ol>");
      while (i < lines.length && /^[\s]*\d+[.)]\s/.test(lines[i])) {
        out.push("<li>" + inlinePass(lines[i].replace(/^[\s]*\d+[.)]\s+/, "")) + "</li>");
        i++;
      }
      out.push("</ol>");
      continue;
    }

    // Simple table (| col | col |)
    if (/^\|(.+)\|/.test(line)) {
      var tableLines: string[] = [];
      while (i < lines.length && /^\|(.+)\|/.test(lines[i])) {
        tableLines.push(lines[i]);
        i++;
      }
      // Skip separator row (| --- | --- |)
      var headerRow = tableLines[0];
      var dataStart = 1;
      if (tableLines.length > 1 && /^\|[\s\-:|]+\|$/.test(tableLines[1])) {
        dataStart = 2;
      }
      out.push("<table>");
      // Header
      var headerCells = headerRow.split("|").filter(function (c) { return c.trim() !== ""; });
      out.push("<tr>");
      for (var h = 0; h < headerCells.length; h++) {
        out.push("<th>" + inlinePass(headerCells[h].trim()) + "</th>");
      }
      out.push("</tr>");
      // Body rows
      for (var r = dataStart; r < tableLines.length; r++) {
        var cells = tableLines[r].split("|").filter(function (c) { return c.trim() !== ""; });
        out.push("<tr>");
        for (var c = 0; c < cells.length; c++) {
          out.push("<td>" + inlinePass(cells[c].trim()) + "</td>");
        }
        out.push("</tr>");
      }
      out.push("</table>");
      continue;
    }

    // Paragraph — collect consecutive non-blank, non-block lines
    var pLines: string[] = [];
    while (
      i < lines.length &&
      lines[i].trim() !== "" &&
      !lines[i].match(/^```/) &&
      !lines[i].match(/^#{1,6}\s/) &&
      !lines[i].match(/^>\s?/) &&
      !lines[i].match(/^[\s]*[-*+]\s/) &&
      !lines[i].match(/^[\s]*\d+[.)]\s/) &&
      !lines[i].match(/^\|(.+)\|/) &&
      !/^(\*{3,}|-{3,}|_{3,})\s*$/.test(lines[i])
    ) {
      pLines.push(lines[i]);
      i++;
    }
    if (pLines.length > 0) {
      out.push("<p>" + inlinePass(pLines.join("<br>")) + "</p>");
    }
  }

  return out.join("");
}
