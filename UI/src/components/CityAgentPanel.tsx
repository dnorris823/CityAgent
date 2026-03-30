import React, { Component, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { bindValue, useValue, trigger } from "cs2/api";
import { renderMarkdown } from "../utils/renderMarkdown";
import { formatRelativeTime } from "../utils/formatRelativeTime";

interface ChatMessage {
  role: "user" | "assistant" | "system";
  content: string;
  hadImage: boolean;
}

// Lazy-initialize bindings on first component render instead of at module scope.
// This prevents the module from crashing if cs2/api isn't ready at import time.
let panelVisible$:     any = null;
let messagesJson$:     any = null;
let isLoading$:        any = null;
let hasScreenshot$:    any = null;
let panelWidth$:       any = null;
let panelHeight$:      any = null;
let fontSize$:         any = null;
let memoryFilesJson$:  any = null;
let memoryOpResult$:   any = null;
let bindingsReady = false;
let bindError: string | null = null;

function ensureBindings() {
  if (bindingsReady) return;
  try {
    panelVisible$    = bindValue<boolean>("cityAgent", "panelVisible");
    messagesJson$    = bindValue<string> ("cityAgent", "messagesJson");
    isLoading$       = bindValue<boolean>("cityAgent", "isLoading");
    hasScreenshot$   = bindValue<boolean>("cityAgent", "hasScreenshot");
    panelWidth$      = bindValue<number> ("cityAgent", "panelWidth");
    panelHeight$     = bindValue<number> ("cityAgent", "panelHeight");
    fontSize$        = bindValue<number> ("cityAgent", "fontSize");
    memoryFilesJson$ = bindValue<string> ("cityAgent", "memoryFilesJson");
    memoryOpResult$  = bindValue<string> ("cityAgent", "memoryOpResult");
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
  const panelVisible    = useValue(panelVisible$)    as boolean || false;
  const rawJson         = useValue(messagesJson$)    as string  || "[]";
  const isLoading       = useValue(isLoading$)       as boolean || false;
  const hasScreenshot   = useValue(hasScreenshot$)   as boolean || false;
  const panelWidth      = (useValue(panelWidth$)     as number) || 520;
  const panelHeight     = (useValue(panelHeight$)    as number) || 650;
  const fontSize        = (useValue(fontSize$)       as number) || 14;
  var memoryFilesRaw    = useValue(memoryFilesJson$) as string  || "[]";
  var memoryOpResult    = useValue(memoryOpResult$)  as string  || "";

  const [inputText, setInputText] = useState("");
  const [dragPos, setDragPos] = useState<{ x: number; y: number } | null>(null);
  const [resizedDims, setResizedDims] = useState<{ w: number; h: number } | null>(null);

  // Phase 2: queued message (type-ahead while loading) — stored as ref to avoid double-send (D-09/D-10)
  const pendingQueuedMsg = useRef<string | null>(null);
  const [queuedChipText, setQueuedChipText] = useState<string | null>(null);

  // Phase 2: loading status text (D-06) — phrase index driven by interval
  const LOADING_PHRASES = [
    "Surveying the city...",
    "Consulting the records...",
    "Studying the districts...",
    "Reviewing the latest reports...",
    "Checking in with the planners...",
    "Analyzing the situation...",
  ];
  const [loadingPhrase, setLoadingPhrase] = useState(LOADING_PHRASES[0]);

  // Phase 2: welcome greeting (D-11) — selected randomly on mount and on clearChat
  const WELCOME_GREETINGS = [
    "Welcome back, Mayor. The city awaits.",
    "Your advisor is ready. What would you like to know?",
    "The city has been busy. Ask me anything.",
    "Ready to help. What's on your mind, Mayor?",
    "All systems nominal. How can I assist?",
  ];
  const [welcomeGreeting] = useState(
    () => WELCOME_GREETINGS[Math.floor(Math.random() * WELCOME_GREETINGS.length)]
  );

  // Phase 5: Tab navigation state
  var [activeTab, setActiveTab]                     = useState<'advisor' | 'memory'>('advisor');
  var [memoryView, setMemoryView]                   = useState<'list' | 'file'>('list');
  var [selectedFile, setSelectedFile]               = useState<string>('');
  var [selectedFileIsCore, setSelectedFileIsCore]   = useState<boolean>(false);
  var [isEditing, setIsEditing]                     = useState<boolean>(false);
  var [editContent, setEditContent]                 = useState<string>('');
  var [fileContent, setFileContent]                 = useState<string>('');
  var [isDeleteConfirm, setIsDeleteConfirm]         = useState<boolean>(false);
  var [memoryError, setMemoryError]                 = useState<string>('');
  var [awaitingOp, setAwaitingOp]                   = useState<'read' | 'write' | 'delete' | null>(null);

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

  // Phase 5: parse and sort memory files
  var memoryFiles = useMemo(function() {
    try {
      var parsed = JSON.parse(memoryFilesRaw);
      if (!Array.isArray(parsed)) return [];
      // Sort: core first (alphabetical), then non-core (alphabetical)
      parsed.sort(function(a: any, b: any) {
        if (a.is_core && !b.is_core) return -1;
        if (!a.is_core && b.is_core) return 1;
        return (a.name || '').localeCompare(b.name || '');
      });
      return parsed;
    } catch (e) { return []; }
  }, [memoryFilesRaw]);

  // Auto-scroll to bottom when messages change.
  const messagesContainerRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const el = messagesContainerRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [messages, isLoading]);

  // Phase 2: Cycle loading status phrases while isLoading is true (D-06)
  // Pitfall: clearInterval in cleanup — otherwise setState fires on unmounted path
  useEffect(() => {
    if (!isLoading) {
      setLoadingPhrase(LOADING_PHRASES[0]); // reset for next load
      return;
    }
    const id = setInterval(() => {
      setLoadingPhrase(LOADING_PHRASES[Math.floor(Date.now() / 2500) % LOADING_PHRASES.length]);
    }, 2500);
    return () => clearInterval(id);
  }, [isLoading]);

  // Phase 2: Auto-send queued message when loading finishes (D-10)
  // Pitfall: Use ref (not state) for the queued message to avoid double-send on re-render
  useEffect(() => {
    if (!isLoading && pendingQueuedMsg.current) {
      const msg = pendingQueuedMsg.current;
      pendingQueuedMsg.current = null;
      setQueuedChipText(null);
      safeTrigger("cityAgent", "sendMessage", msg);
    }
  }, [isLoading]);

  // Phase 5: Handle memoryOpResult changes — dispatch based on awaitingOp
  useEffect(function() {
    if (!memoryOpResult || memoryOpResult === "" || awaitingOp === null) return;

    if (awaitingOp === 'read') {
      setFileContent(memoryOpResult);
      setAwaitingOp(null);
    } else if (awaitingOp === 'write') {
      if (memoryOpResult === "ok" || memoryOpResult.indexOf("Successfully wrote") === 0) {
        // Save succeeded — switch to view mode, use the content we sent
        setFileContent(editContent);
        setIsEditing(false);
        setMemoryError('');
      } else {
        // Save failed — stay in edit mode, show error
        var errMsg = memoryOpResult.replace(/^\[Error\]:\s*/, '');
        if (errMsg.length > 120) errMsg = errMsg.substring(0, 120) + '...';
        setMemoryError('Save failed \u2014 ' + errMsg);
      }
      setAwaitingOp(null);
    } else if (awaitingOp === 'delete') {
      if (memoryOpResult === "ok" || memoryOpResult.indexOf("Successfully deleted") === 0) {
        // Delete succeeded — go back to list, refresh
        setMemoryView('list');
        setSelectedFile('');
        setIsDeleteConfirm(false);
        setMemoryError('');
        safeTrigger("cityAgent", "refreshMemoryFiles");
      } else {
        var delErr = memoryOpResult.replace(/^\[Error\]:\s*/, '');
        if (delErr.length > 120) delErr = delErr.substring(0, 120) + '...';
        setMemoryError('Delete failed \u2014 ' + delErr);
        setIsDeleteConfirm(false);
      }
      setAwaitingOp(null);
    }
  }, [memoryOpResult]);

  const canSend = inputText.trim().length > 0 && (isLoading ? pendingQueuedMsg.current === null : true);

  const handleSend = useCallback(() => {
    const text = inputText.trim();
    if (!text) return;
    if (isLoading) {
      // Queue the message — it will auto-send when loading finishes (D-09)
      pendingQueuedMsg.current = text;
      setQueuedChipText(text);
      setInputText("");
      console.log("[CityAgent] Message queued (loading in progress):", text.substring(0, 80));
    } else {
      console.log("[CityAgent] Sending message:", text.substring(0, 80));
      safeTrigger("cityAgent", "sendMessage", text);
      setInputText("");
    }
  }, [inputText, isLoading]);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  // Phase 5: Tab and memory explorer handlers
  function handleTabSwitch(tab: 'advisor' | 'memory') {
    setActiveTab(tab);
    if (tab === 'memory') {
      // Reset memory state per D-04
      setMemoryView('list');
      setSelectedFile('');
      setIsEditing(false);
      setIsDeleteConfirm(false);
      setMemoryError('');
      setFileContent('');
      safeTrigger("cityAgent", "refreshMemoryFiles");
    }
  }

  function handleFileClick(file: any) {
    setSelectedFile(file.name);
    setSelectedFileIsCore(file.is_core);
    setMemoryView('file');
    setIsEditing(false);
    setIsDeleteConfirm(false);
    setMemoryError('');
    setFileContent('');
    setAwaitingOp('read');
    safeTrigger("cityAgent", "readMemoryFile", file.name);
  }

  function handleBackToList() {
    setMemoryView('list');
    setSelectedFile('');
    setIsEditing(false);
    setIsDeleteConfirm(false);
    setMemoryError('');
  }

  function handleEditStart() {
    setEditContent(fileContent);
    setIsEditing(true);
    setMemoryError('');
  }

  function handleEditCancel() {
    setIsEditing(false);
    setMemoryError('');
  }

  function handleEditSave() {
    setMemoryError('');
    setAwaitingOp('write');
    safeTrigger("cityAgent", "writeMemoryFile", selectedFile, editContent);
  }

  function handleDeleteStart() {
    setIsDeleteConfirm(true);
    setMemoryError('');
  }

  function handleDeleteCancel() {
    setIsDeleteConfirm(false);
  }

  function handleDeleteConfirm() {
    setMemoryError('');
    setAwaitingOp('delete');
    safeTrigger("cityAgent", "deleteMemoryFile", selectedFile);
  }

  function formatFileSize(sizeKb: number): string {
    if (sizeKb < 1) return Math.round(sizeKb * 1024) + 'B';
    return sizeKb.toFixed(1) + 'KB';
  }

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
            <div className="ca-tabs" onMouseDown={stopDragPropagation}>
              <button
                className={"ca-tabs__tab" + (activeTab === 'advisor' ? " ca-tabs__tab--active" : "")}
                onClick={function() { handleTabSwitch('advisor'); }}
              >Advisor</button>
              <button
                className={"ca-tabs__tab" + (activeTab === 'memory' ? " ca-tabs__tab--active" : "")}
                onClick={function() { handleTabSwitch('memory'); }}
              >Memory</button>
            </div>
            <div className="ca-panel__header-actions" onMouseDown={stopDragPropagation}>
              {activeTab === 'advisor' && (
                <button className="ca-btn-icon ca-btn-new-chat" onClick={function() { safeTrigger("cityAgent", "clearChat"); }}>
                  + New Chat
                </button>
              )}
              <button className="ca-btn-icon" onClick={() => safeTrigger("cityAgent", "togglePanel")}>
                &#x2715;
              </button>
            </div>
          </header>

          {activeTab === 'advisor' && (
            <>
              <div className="ca-messages" ref={messagesContainerRef}>
                {messages.map((msg, i) => {
                  if (msg.role === "system") {
                    // System notices render as center pills, not bubbles (D-01)
                    const isError = msg.content.startsWith("[Error]:");
                    const pillClass = isError ? "ca-notice-pill ca-notice-pill--error" : "ca-notice-pill ca-notice-pill--warning";
                    const text = msg.content.replace(/^\[Error\]:\s*/, "");
                    return (
                      <div key={i} className={pillClass}>
                        {text}
                      </div>
                    );
                  }
                  return (
                    <div key={i} className={`ca-bubble ca-bubble--${msg.role}`}>
                      {msg.hadImage && (
                        <span className="ca-bubble__image-badge">screenshot attached</span>
                      )}
                      {msg.role === "assistant" ? (
                        <div className="ca-bubble__text ca-markdown" dangerouslySetInnerHTML={{ __html: renderMarkdown(msg.content) }} />
                      ) : (
                        <span className="ca-bubble__text">{msg.content}</span>
                      )}
                    </div>
                  );
                })}
                {isLoading && (
                  <div className="ca-bubble ca-bubble--assistant">
                    <LoadingDots />
                    <span className="ca-loading-status">{loadingPhrase}</span>
                  </div>
                )}
                {messages.length === 0 && !isLoading && (
                  <div className="ca-welcome">{welcomeGreeting}</div>
                )}
              </div>

              <div className="ca-input-area">
                {hasScreenshot && (
                  <div className="ca-screenshot-chip">
                    <span>screenshot ready</span>
                    <button onClick={() => safeTrigger("cityAgent", "removeScreenshot")}>&#x2715;</button>
                  </div>
                )}
                {queuedChipText && (
                  <div className="ca-queued-chip">
                    <span>queued: {queuedChipText.length > 40 ? queuedChipText.substring(0, 40) + "..." : queuedChipText}</span>
                    <button onClick={() => {
                      pendingQueuedMsg.current = null;
                      setQueuedChipText(null);
                    }}>&#x2715;</button>
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
            </>
          )}

          {activeTab === 'memory' && memoryView === 'list' && (
            <div className="ca-mem-list">
              {memoryFiles.length === 0 ? (
                <div className="ca-mem-list__empty">No memory files found.</div>
              ) : (
                memoryFiles.map(function(file: any) {
                  return (
                    <div key={file.name} className="ca-mem-list__row" onClick={function() { handleFileClick(file); }}>
                      <div className="ca-mem-list__icon">
                        {file.is_core && <span className="ca-mem-badge--core">core</span>}
                      </div>
                      <span className={"ca-mem-list__name" + (file.is_core ? " ca-mem-list__name--core" : "")}>
                        {file.name}
                      </span>
                      <div className="ca-mem-list__meta">
                        <span className="ca-mem-list__size">{formatFileSize(file.size_kb)}</span>
                        <span className="ca-mem-list__time">{formatRelativeTime(file.last_modified_unix)}</span>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          )}

          {activeTab === 'memory' && memoryView === 'file' && (
            <>
              {/* Sub-header */}
              <div className={"ca-mem-subheader" + (isDeleteConfirm ? " ca-mem-subheader--destructive" : "")}
                   onMouseDown={stopDragPropagation}>
                {isDeleteConfirm ? (
                  <>
                    <span className="ca-mem-subheader__filename">Delete {selectedFile}?</span>
                    {memoryError && <span className="ca-mem-subheader__error">{memoryError}</span>}
                    <div className="ca-mem-subheader__actions">
                      <button className="ca-btn-icon ca-btn-icon--destructive" onClick={handleDeleteConfirm}>Yes</button>
                      <button className="ca-btn-icon" onClick={handleDeleteCancel}>Discard Changes</button>
                    </div>
                  </>
                ) : (
                  <>
                    <button className="ca-mem-subheader__back" onClick={handleBackToList}>{'\u2190'}</button>
                    <span className="ca-mem-subheader__filename">{selectedFile}</span>
                    {memoryError && !isEditing && <span className="ca-mem-subheader__error">{memoryError}</span>}
                    <div className="ca-mem-subheader__actions">
                      {isEditing ? (
                        <>
                          <button className="ca-btn-icon" onClick={handleEditSave}>Save Changes</button>
                          <button className="ca-btn-icon" onClick={handleEditCancel}>Discard Changes</button>
                        </>
                      ) : (
                        <>
                          <button className="ca-btn-icon" onClick={handleEditStart}>Edit</button>
                          {!selectedFileIsCore && (
                            <button className="ca-btn-icon" onClick={handleDeleteStart}>Delete</button>
                          )}
                          {selectedFileIsCore && <span className="ca-mem-badge--core">core</span>}
                        </>
                      )}
                    </div>
                  </>
                )}
              </div>

              {/* Error notice for save failures (above textarea) */}
              {isEditing && memoryError && (
                <div className="ca-mem-error">{memoryError}</div>
              )}

              {/* Content area */}
              {isEditing ? (
                <textarea
                  className="ca-mem-textarea"
                  value={editContent}
                  onChange={function(e: any) { setEditContent(e.target.value); }}
                />
              ) : (
                fileContent === '' && awaitingOp === 'read' ? (
                  <div className="ca-mem-content ca-mem-content--loading">Loading...</div>
                ) : (
                  <div className="ca-mem-content">{fileContent}</div>
                )
              )}
            </>
          )}
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
