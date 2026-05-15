import { useState, useRef, useEffect, useCallback } from 'react'
import * as signalR from '@microsoft/signalr'
import ReactMarkdown from 'react-markdown'
import rehypeRaw from 'rehype-raw'
import { useResizable } from '../hooks/useResizable'

const HUB_URL = (import.meta.env.VITE_API_URL || 'http://localhost:5000') + '/hub/chat'

export default function ChatSideBar({ isOpen, onClose, graphId, graphName, selectedNodeTitle }) {
  // messages: { role: 'user'|'assistant', text: string }
  const [messages, setMessages] = useState([])
  // history in the format the server expects: { role, content }
  const historyRef = useRef([])
  const [input, setInput] = useState('')
  const [connStatus, setConnStatus] = useState('disconnected') // 'disconnected'|'connecting'|'connected'|'error'
  const [streaming, setStreaming] = useState(false)
  const streamBufferRef = useRef('')
  const connectionRef = useRef(null)
  const bodyRef = useRef(null)
  const textareaRef = useRef(null)

  // Auto-scroll on new content
  useEffect(() => {
    if (bodyRef.current) bodyRef.current.scrollTop = bodyRef.current.scrollHeight
  }, [messages, streaming])

  // Build and start connection
  useEffect(() => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => localStorage.getItem('token') ?? '',
      })
      .withAutomaticReconnect()
      .build()

    conn.on('ReceiveChunk', (chunk) => {
      streamBufferRef.current += chunk
      const buf = streamBufferRef.current
      setMessages(prev => {
        const last = prev[prev.length - 1]
        if (last?.role === 'assistant' && last.streaming) {
          return [...prev.slice(0, -1), { ...last, text: buf }]
        }
        return [...prev, { role: 'assistant', text: buf, streaming: true }]
      })
    })

    conn.on('ResponseComplete', () => {
      const completed = streamBufferRef.current
      streamBufferRef.current = ''
      setStreaming(false)
      setMessages(prev => {
        const last = prev[prev.length - 1]
        if (last?.role === 'assistant') {
          const finalMsg = { role: 'assistant', text: completed }
          historyRef.current.push({ role: 'assistant', content: completed })
          return [...prev.slice(0, -1), finalMsg]
        }
        return prev
      })
    })

    conn.onreconnecting(err => {
      setConnStatus('connecting')
      if (err) console.warn('[SignalR] Reconnecting:', err.message)
    })
    conn.onreconnected(() => setConnStatus('connected'))
    conn.onclose(err => {
      setConnStatus('disconnected')
      if (err) console.error('[SignalR] Connection closed:', err.message)
    })

    setConnStatus('connecting')
    conn.start()
      .then(() => setConnStatus('connected'))
      .catch(err => {
        setConnStatus('error')
        console.error('[SignalR] Failed to connect:', err.message)
      })

    connectionRef.current = conn

    return () => { conn.stop() }
  }, [])

  const handleSend = useCallback(async () => {
    const text = input.trim()
    if (!text || streaming || connStatus !== 'connected') return

    const userMsg = { role: 'user', content: text }
    historyRef.current.push(userMsg)

    setMessages(prev => [...prev, { role: 'user', text }])
    setInput('')
    setStreaming(true)
    streamBufferRef.current = ''
    if (textareaRef.current) textareaRef.current.style.height = 'auto'

    try {
      await connectionRef.current.invoke('SendMessage', graphId, historyRef.current)
    } catch (err) {
      setStreaming(false)
      const detail = err?.message || String(err)
      console.error('[SignalR] SendMessage failed:', detail)
      const label = detail.includes('Unauthorized') || detail.includes('401')
        ? '⚠ Unauthorized — check your session.'
        : detail.includes('HubException')
          ? `⚠ Server error: ${detail.replace(/.*HubException:\s*/i, '')}`
          : `⚠ Failed to send: ${detail}`
      setMessages(prev => [...prev, { role: 'assistant', text: label, error: true }])
      // Remove the failed user message from history so it doesn't corrupt future sends
      historyRef.current.pop()
    }
  }, [input, streaming, connStatus, graphId])

  function handleKeyDown(e) {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault()
      handleSend()
    }
  }

  function handleInput(e) {
    setInput(e.target.value)
    const el = e.target
    el.style.height = 'auto'
    el.style.height = Math.min(el.scrollHeight, 120) + 'px'
  }

  function handleClear() {
    setMessages([])
    historyRef.current = []
    streamBufferRef.current = ''
    setStreaming(false)
  }

  const statusLabel = {
    disconnected: 'disconnected',
    connecting: 'connecting…',
    connected: 'ready',
    error: 'connection error',
  }[connStatus]

  const { width, onMouseDown: onResizeMouseDown } = useResizable(500)
  const ctx = selectedNodeTitle || graphName || 'your graph'
  const canSend = input.trim() && !streaming && connStatus === 'connected'

  return (
    <aside className={`chat-panel${isOpen ? ' open' : ''}`} style={{ width }}>
      <div className="side-panel-resize-handle" onMouseDown={onResizeMouseDown} />
      <div className="chat-head">
        <span className="chat-title">AI Assistant</span>
        <span className="chat-ctx"> — <b>{ctx}</b></span>
        <button className="chat-icon-btn" onClick={handleClear} title="Clear conversation">
          <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
            <path d="M3 5L13 5" /><path d="M5 5L5 13A1 1 0 006 14L10 14A1 1 0 0011 13L11 5" /><path d="M6 5L6 3A1 1 0 017 2L9 2A1 1 0 0110 3L10 5" />
          </svg>
        </button>
        <button className="chat-icon-btn" onClick={onClose} title="Close">
          <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round">
            <line x1="4" y1="4" x2="12" y2="12" /><line x1="12" y1="4" x2="4" y2="12" />
          </svg>
        </button>
      </div>

      <div className="chat-body" ref={bodyRef}>
        {messages.length === 0 && (
          <p className="chat-empty">Ask anything about your notes…</p>
        )}
        {messages.map((m, i) => (
          <div key={i} className={`chat-msg ${m.role}`}>
            <div className="chat-avatar">{m.role === 'assistant' ? 'AI' : 'U'}</div>
            <div className={`chat-bubble${m.error ? ' chat-bubble--error' : ''}`}>
              {m.role === 'assistant' && !m.error
                ? <><ReactMarkdown rehypePlugins={[rehypeRaw]}>{m.text}</ReactMarkdown>{m.streaming && <span className="chat-cursor" />}</>
                : m.text
              }
            </div>
          </div>
        ))}
        {streaming && messages[messages.length - 1]?.role !== 'assistant' && (
          <div className="chat-msg assistant">
            <div className="chat-avatar">AI</div>
            <div className="chat-bubble">
              <span className="chat-typing">
                <span /><span /><span />
              </span>
            </div>
          </div>
        )}
      </div>

      <div className="chat-input-wrap">
        <div className="chat-input-row">
          <textarea
            ref={textareaRef}
            value={input}
            onChange={handleInput}
            onKeyDown={handleKeyDown}
            placeholder="ask anything about your notes..."
            rows={1}
            disabled={connStatus !== 'connected'}
          />
          <button className="chat-send" onClick={handleSend} disabled={!canSend} title="Send (Enter)">
            <svg viewBox="0 0 14 14" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
              <line x1="2" y1="7" x2="12" y2="7" /><path d="M8 3L12 7L8 11" />
            </svg>
          </button>
        </div>
        <div className="chat-foot">
          <span><kbd>↵</kbd> send · <kbd>⇧↵</kbd> newline</span>
          <span className={`chat-status chat-status--${connStatus}`}>{statusLabel}</span>
        </div>
      </div>
    </aside>
  )
}
