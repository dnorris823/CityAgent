import React, { Component, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { bindValue, useValue, trigger } from "cs2/api";

interface ChatMessage {
  role: "user" | "assistant";
  content: string;
  hadImage: boolean;
}

// Lazy-initialize bindings on first component render instead of at module scope.
// This prevents the module from crashing if cs2/api isn't ready at import time.
let panelVisible$:  any = null;
let messagesJson$:  any = null;
let isLoading$:     any = null;
let hasScreenshot$: any = null;
let bindingsReady = false;
let bindError: string | null = null;

function ensureBindings() {
  if (bindingsReady) return;
  try {
    panelVisible$  = bindValue<boolean>("cityAgent", "panelVisible");
    messagesJson$  = bindValue<string> ("cityAgent", "messagesJson");
    isLoading$     = bindValue<boolean>("cityAgent", "isLoading");
    hasScreenshot$ = bindValue<boolean>("cityAgent", "hasScreenshot");
    bindingsReady = true;
  } catch (e: any) {
    bindError = e?.message || "Unknown binding error";
  }
}

// Error boundary — catches render errors so the whole UI doesn't vanish.
const errorBoxStyle: React.CSSProperties = {
  position: "fixed",
  bottom: "80px",
  right: "20px",
  zIndex: 99999,
  background: "#c00",
  color: "white",
  padding: "8px 16px",
  border: "none",
  borderRadius: "6px",
  fontSize: "13px",
  fontFamily: "inherit",
  maxWidth: "400px",
  wordBreak: "break-word" as any,
};

class ErrorBoundary extends Component<
  { children: React.ReactNode },
  { error: string | null }
> {
  state = { error: null as string | null };

  static getDerivedStateFromError(err: any) {
    return { error: String(err?.message || err || "Unknown render error") };
  }

  componentDidCatch(err: any, info: any) {
    console.error("[CityAgent] React error boundary caught:", err, info);
  }

  render() {
    if (this.state.error) {
      return (
        <div style={errorBoxStyle}>
          CityAgent crashed: {this.state.error}
          <br />
          <button
            style={{ marginTop: "6px", cursor: "pointer" }}
            onClick={() => this.setState({ error: null })}
          >
            Retry
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}

const LoadingDots: React.FC = () => (
  <div className="ca-loading-dots">
    <span /><span /><span />
  </div>
);

function safeTrigger(group: string, name: string, ...args: any[]) {
  try {
    trigger(group, name, ...args);
  } catch (e: any) {
    console.error(`[CityAgent] trigger("${group}","${name}") failed:`, e);
  }
}

// Inner component — only rendered after bindings are confirmed ready.
// All hooks live here so they're called unconditionally.
const CityAgentInner: React.FC = () => {
  const panelVisible  = useValue(panelVisible$)  as boolean || false;
  const rawJson       = useValue(messagesJson$)  as string  || "[]";
  const isLoading     = useValue(isLoading$)     as boolean || false;
  const hasScreenshot = useValue(hasScreenshot$) as boolean || false;

  const [inputText, setInputText] = useState("");

  const messages = useMemo<ChatMessage[]>(() => {
    try {
      const parsed = JSON.parse(rawJson);
      return Array.isArray(parsed) ? parsed : [];
    }
    catch { return []; }
  }, [rawJson]);

  // Auto-scroll to bottom when messages change.
  // scrollIntoView is not available in Coherent GT, so we set scrollTop directly.
  const messagesContainerRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const el = messagesContainerRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [messages, isLoading]);

  const canSend = inputText.trim().length > 0 && !isLoading;

  const handleSend = useCallback(() => {
    if (!canSend) return;
    console.log("[CityAgent] Sending message:", inputText.trim().substring(0, 80));
    safeTrigger("cityAgent", "sendMessage", inputText.trim());
    setInputText("");
  }, [inputText, canSend]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  return (
    <>
      <button className="ca-toggle-btn" onClick={() => safeTrigger("cityAgent", "togglePanel")}>
        CityAgent
      </button>

      {panelVisible && (
        <div className="ca-panel">
          <header className="ca-panel__header">
            <span>CityAgent AI Advisor</span>
            <div className="ca-panel__header-actions">
              <button className="ca-btn-icon" onClick={() => safeTrigger("cityAgent", "clearChat")}>
                Clear
              </button>
              <button className="ca-btn-icon" onClick={() => safeTrigger("cityAgent", "togglePanel")}>
                ✕
              </button>
            </div>
          </header>

          <div className="ca-messages" ref={messagesContainerRef}>
            {messages.map((msg, i) => (
              <div key={i} className={`ca-bubble ca-bubble--${msg.role}`}>
                {msg.hadImage && (
                  <span className="ca-bubble__image-badge">screenshot attached</span>
                )}
                <span className="ca-bubble__text">{msg.content}</span>
              </div>
            ))}
            {isLoading && (
              <div className="ca-bubble ca-bubble--assistant">
                <LoadingDots />
              </div>
            )}
          </div>

          <div className="ca-input-area">
            {hasScreenshot && (
              <div className="ca-screenshot-chip">
                <span>screenshot ready</span>
                <button onClick={() => safeTrigger("cityAgent", "removeScreenshot")}>✕</button>
              </div>
            )}
            <div className="ca-input-row">
              <textarea
                className="ca-input"
                value={inputText}
                onChange={e => setInputText(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Ask about your city..."
                rows={2}
              />
              <button
                className="ca-screenshot-btn"
                onClick={() => safeTrigger("cityAgent", "captureScreenshot")}
                disabled={hasScreenshot || isLoading}
                title="Capture screenshot"
              >
                SS
              </button>
              <button
                className="ca-send-btn"
                onClick={handleSend}
                disabled={!canSend}
              >
                Send
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
};

// Outer wrapper: initializes bindings, shows error fallback if they fail.
// No hooks here so we can safely return early.
export const CityAgentPanel: React.FC = () => {
  ensureBindings();

  if (bindError) {
    return (
      <div style={errorBoxStyle}>
        CityAgent Error: {bindError}
      </div>
    );
  }

  return (
    <ErrorBoundary>
      <CityAgentInner />
    </ErrorBoundary>
  );
};
