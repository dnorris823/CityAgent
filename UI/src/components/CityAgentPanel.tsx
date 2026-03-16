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
let panelWidth$:    any = null;
let panelHeight$:   any = null;
let fontSize$:      any = null;
let bindingsReady = false;
let bindError: string | null = null;

function ensureBindings() {
  if (bindingsReady) return;
  try {
    panelVisible$  = bindValue<boolean>("cityAgent", "panelVisible");
    messagesJson$  = bindValue<string> ("cityAgent", "messagesJson");
    isLoading$     = bindValue<boolean>("cityAgent", "isLoading");
    hasScreenshot$ = bindValue<boolean>("cityAgent", "hasScreenshot");
    panelWidth$    = bindValue<number> ("cityAgent", "panelWidth");
    panelHeight$   = bindValue<number> ("cityAgent", "panelHeight");
    fontSize$      = bindValue<number> ("cityAgent", "fontSize");
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
  const panelWidth    = (useValue(panelWidth$)   as number) || 520;
  const panelHeight   = (useValue(panelHeight$)  as number) || 650;
  const fontSize      = (useValue(fontSize$)     as number) || 14;

  const [inputText, setInputText] = useState("");
  const [dragPos, setDragPos] = useState<{ x: number; y: number } | null>(null);
  const [resizedDims, setResizedDims] = useState<{ w: number; h: number } | null>(null);

  // Reset drag position and local resize when settings-based dimensions change
  const prevBindingDims = useRef({ w: panelWidth, h: panelHeight });
  useEffect(() => {
    if (prevBindingDims.current.w !== panelWidth || prevBindingDims.current.h !== panelHeight) {
      prevBindingDims.current = { w: panelWidth, h: panelHeight };
      setDragPos(null);
      setResizedDims(null);
    }
  }, [panelWidth, panelHeight]);

  // Effective dimensions: local resize overrides binding defaults
  const effectiveWidth  = resizedDims ? resizedDims.w : panelWidth;
  const effectiveHeight = resizedDims ? resizedDims.h : panelHeight;

  const messages = useMemo<ChatMessage[]>(() => {
    try {
      const parsed = JSON.parse(rawJson);
      return Array.isArray(parsed) ? parsed : [];
    }
    catch { return []; }
  }, [rawJson]);

  // Auto-scroll to bottom when messages change.
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

  // ── Drag logic ──
  const dragRef = useRef<{ startX: number; startY: number; origX: number; origY: number } | null>(null);

  const handleHeaderMouseDown = useCallback((e: React.MouseEvent) => {
    const defaultX = window.innerWidth - effectiveWidth - 20;
    const defaultY = window.innerHeight - effectiveHeight - 130;
    const currentX = dragPos ? dragPos.x : defaultX;
    const currentY = dragPos ? dragPos.y : defaultY;

    dragRef.current = {
      startX: e.clientX,
      startY: e.clientY,
      origX: currentX,
      origY: currentY,
    };

    const onMouseMove = (ev: MouseEvent) => {
      if (!dragRef.current) return;
      const dx = ev.clientX - dragRef.current.startX;
      const dy = ev.clientY - dragRef.current.startY;
      let newX = dragRef.current.origX + dx;
      let newY = dragRef.current.origY + dy;

      const minVisible = 100;
      newX = Math.max(minVisible - effectiveWidth, Math.min(window.innerWidth - minVisible, newX));
      newY = Math.max(0, Math.min(window.innerHeight - minVisible, newY));

      setDragPos({ x: newX, y: newY });
    };

    const onMouseUp = () => {
      dragRef.current = null;
      document.removeEventListener("mousemove", onMouseMove);
      document.removeEventListener("mouseup", onMouseUp);
    };

    document.addEventListener("mousemove", onMouseMove);
    document.addEventListener("mouseup", onMouseUp);
  }, [dragPos, effectiveWidth, effectiveHeight]);

  // ── Resize logic ──
  const MIN_W = 300;
  const MIN_H = 250;

  const resizeRef = useRef<{
    edge: string;
    startX: number;
    startY: number;
    origW: number;
    origH: number;
    origPosX: number;
    origPosY: number;
  } | null>(null);

  const handleResizeMouseDown = useCallback((edge: string, e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();

    const defaultX = window.innerWidth - effectiveWidth - 20;
    const defaultY = window.innerHeight - effectiveHeight - 130;

    resizeRef.current = {
      edge,
      startX: e.clientX,
      startY: e.clientY,
      origW: effectiveWidth,
      origH: effectiveHeight,
      origPosX: dragPos ? dragPos.x : defaultX,
      origPosY: dragPos ? dragPos.y : defaultY,
    };

    const onMouseMove = (ev: MouseEvent) => {
      if (!resizeRef.current) return;
      const r = resizeRef.current;
      const dx = ev.clientX - r.startX;
      const dy = ev.clientY - r.startY;

      let newW = r.origW;
      let newH = r.origH;
      let newX = r.origPosX;
      let newY = r.origPosY;

      if (r.edge === "right" || r.edge === "corner") {
        newW = Math.max(MIN_W, r.origW + dx);
      }
      if (r.edge === "bottom" || r.edge === "corner") {
        newH = Math.max(MIN_H, r.origH + dy);
      }
      if (r.edge === "left") {
        const dw = Math.min(dx, r.origW - MIN_W);
        newW = r.origW - dw;
        newX = r.origPosX + dw;
      }
      if (r.edge === "top") {
        const dh = Math.min(dy, r.origH - MIN_H);
        newH = r.origH - dh;
        newY = r.origPosY + dh;
      }

      setResizedDims({ w: newW, h: newH });
      setDragPos({ x: newX, y: newY });
    };

    const onMouseUp = () => {
      resizeRef.current = null;
      document.removeEventListener("mousemove", onMouseMove);
      document.removeEventListener("mouseup", onMouseUp);
    };

    document.addEventListener("mousemove", onMouseMove);
    document.addEventListener("mouseup", onMouseUp);
  }, [dragPos, effectiveWidth, effectiveHeight]);

  // ── Position & style ──
  const posX = dragPos ? dragPos.x : window.innerWidth - effectiveWidth - 20;
  const posY = dragPos ? dragPos.y : window.innerHeight - effectiveHeight - 130;

  const panelStyle: React.CSSProperties = {
    width: `${effectiveWidth}px`,
    height: `${effectiveHeight}px`,
    left: `${posX}px`,
    top: `${posY}px`,
    fontSize: `${fontSize}px`,
  };

  const stopDragPropagation = (e: React.MouseEvent) => e.stopPropagation();

  return (
    <>
      <button className="ca-toggle-btn" onClick={() => safeTrigger("cityAgent", "togglePanel")}>
        CityAgent
      </button>

      {panelVisible && (
        <div className="ca-panel" style={panelStyle}>
          {/* Resize handles */}
          <div className="ca-resize-handle ca-resize-handle--right"  onMouseDown={e => handleResizeMouseDown("right", e)} />
          <div className="ca-resize-handle ca-resize-handle--bottom" onMouseDown={e => handleResizeMouseDown("bottom", e)} />
          <div className="ca-resize-handle ca-resize-handle--corner" onMouseDown={e => handleResizeMouseDown("corner", e)} />
          <div className="ca-resize-handle ca-resize-handle--left"   onMouseDown={e => handleResizeMouseDown("left", e)} />
          <div className="ca-resize-handle ca-resize-handle--top"    onMouseDown={e => handleResizeMouseDown("top", e)} />

          <header className="ca-panel__header" onMouseDown={handleHeaderMouseDown}>
            <span className="ca-panel__header-title">CityAgent AI Advisor</span>
            <div className="ca-panel__header-actions" onMouseDown={stopDragPropagation}>
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
