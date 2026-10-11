import { useEffect, useState } from 'react'
import { llmSettingsApi, mcpSettingsApi } from '../../../api'
import {
  SaveButton,
  SecretInput,
  SettingsField,
  SettingsGroup,
  SettingsMessage,
  SettingsToggle,
} from '../primitives'

const PROVIDER_NAMES = { 0: 'Anthropic', 1: 'OpenAI', 2: 'Ollama (Local)' }

function providerName(type) {
  return PROVIDER_NAMES[type] ?? `Provider ${type}`
}

/** Renders backtick-delimited segments of backend-supplied text as inline code. */
function withInlineCode(text) {
  return text.split('`').map((part, i) => (i % 2 ? <code key={i}>{part}</code> : part))
}

/** One provider's key/endpoint persisted to the Electron config for startup injection. */
function ElectronProviderKey({ provider }) {
  const [value, setValue] = useState('')
  const [status, setStatus] = useState('idle')
  const field = provider.requiresEndpoint ? 'endpoint' : 'apiKey'
  const label = provider.requiresEndpoint ? 'Endpoint URL' : 'API Key'
  const placeholder = provider.requiresEndpoint ? 'http://localhost:11434' : 'Paste key…'

  async function handleSave() {
    setStatus('saving')
    const cfg = await window.electronAPI.loadConfig()
    const providerKeys = cfg?.providerKeys || {}
    providerKeys[String(provider.type)] = { ...providerKeys[String(provider.type)], [field]: value }
    await window.electronAPI.saveConfig({ providerKeys })
    setStatus('saved')
    setTimeout(() => setStatus('idle'), 2000)
  }

  return (
    <SettingsField label={`${providerName(provider.type)} — ${label}`}>
      <div className="set-row">
        {provider.requiresEndpoint ? (
          <input
            className="set-input"
            value={value}
            onChange={e => setValue(e.target.value)}
            placeholder={placeholder}
          />
        ) : (
          <SecretInput value={value} onChange={setValue} placeholder={placeholder} />
        )}
        <SaveButton status={status} onClick={handleSave} disabled={!value.trim()} />
      </div>
    </SettingsField>
  )
}

export default function LlmMcpSettings() {
  const [error, setError] = useState('')

  // ── AI provider ────────────────────────────────────────────────────────────
  const [providers, setProviders] = useState([])
  const [current, setCurrent] = useState(null)
  const [provider, setProvider] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [model, setModel] = useState('')
  const [endpoint, setEndpoint] = useState('')
  const [llmStatus, setLlmStatus] = useState('idle')

  // ── MCP ────────────────────────────────────────────────────────────────────
  const [mcp, setMcp] = useState(null)
  const [mcpToggling, setMcpToggling] = useState(false)
  const [mcpConfiguring, setMcpConfiguring] = useState(false)
  const [mcpRegenConfirm, setMcpRegenConfirm] = useState(false)
  const [mcpRegenerating, setMcpRegenerating] = useState(false)
  const [mcpCopied, setMcpCopied] = useState(false)
  const [mcpClientId, setMcpClientId] = useState('claude-desktop')

  useEffect(() => {
    Promise.all([llmSettingsApi.getProviders(), llmSettingsApi.get(), mcpSettingsApi.get()])
      .then(([providerList, currentLlm, mcpSettings]) => {
        setProviders(providerList ?? [])
        setCurrent(currentLlm)
        setProvider(String(currentLlm?.provider ?? ''))
        setModel(currentLlm?.model ?? '')
        setEndpoint(currentLlm?.endpoint ?? '')
        setApiKey('')   // never pre-fill a key
        setMcp(mcpSettings)
      })
      .catch(err => setError(err.message))
  }, [])

  const selected = providers.find(p => String(p.type) === provider)
  const mcpClient = mcp?.clients?.find(c => c.id === mcpClientId) ?? mcp?.clients?.[0]

  async function handleLlmSave() {
    setError('')
    setLlmStatus('saving')
    try {
      const updated = await llmSettingsApi.update({
        provider: Number(provider),
        model: model || null,
        endpoint: endpoint || null,
        // Omitting apiKey preserves the stored one.
        ...(apiKey ? { apiKey } : {}),
      })
      setCurrent(updated)

      // In Electron, mirror the key/endpoint into config.json so the backend gets
      // it as an env var on the next launch.
      if (window.electronAPI && provider) {
        const existing = await window.electronAPI.loadConfig()
        const providerKeys = existing?.providerKeys || {}
        if (apiKey) providerKeys[provider] = { ...providerKeys[provider], apiKey }
        if (endpoint && selected?.requiresEndpoint) providerKeys[provider] = { ...providerKeys[provider], endpoint }
        await window.electronAPI.saveConfig({ providerKeys })
      }

      setApiKey('')
      setLlmStatus('saved')
      setTimeout(() => setLlmStatus('idle'), 2000)
    } catch (err) {
      setError(err.message)
      setLlmStatus('idle')
    }
  }

  async function handleMcpToggle(value) {
    setMcpToggling(true)
    try { setMcp(await mcpSettingsApi.toggle(value)) }
    catch (err) { setError(err.message) }
    finally { setMcpToggling(false) }
  }

  async function handleMcpConfigureUser() {
    setMcpConfiguring(true)
    try { setMcp(await mcpSettingsApi.configureUser()) }
    catch (err) { setError(err.message) }
    finally { setMcpConfiguring(false) }
  }

  async function handleMcpRegenerate() {
    setMcpRegenerating(true)
    try {
      setMcp(await mcpSettingsApi.regenerateKey())
      setMcpRegenConfirm(false)
    } catch (err) { setError(err.message) }
    finally { setMcpRegenerating(false) }
  }

  function handleMcpCopy() {
    navigator.clipboard.writeText(mcpClient?.config ?? '')
    setMcpCopied(true)
    setTimeout(() => setMcpCopied(false), 2000)
  }

  return (
    <>
      <SettingsMessage error={error} />

      <SettingsGroup title="AI Provider" description="The model used for generation, chat and node analysis.">
        <SettingsField
          label="Provider"
          hint={current
            ? `Currently active: ${providerName(current.provider)}${current.model ? ` · ${current.model}` : ''}${current.hasApiKey ? ' · key configured' : ''}`
            : undefined}
        >
          <select
            className="set-input"
            value={provider}
            onChange={e => { setProvider(e.target.value); setModel(''); setApiKey('') }}
          >
            <option value="">Select provider…</option>
            {providers.map(p => (
              <option key={p.type} value={String(p.type)}>
                {providerName(p.type)}{p.defaultModel ? ` — ${p.defaultModel}` : ''}
              </option>
            ))}
          </select>
        </SettingsField>

        {selected && (
          <>
            <SettingsField label="Model">
              {selected.models?.length > 0 ? (
                <select className="set-input" value={model} onChange={e => setModel(e.target.value)}>
                  <option value="">Default ({selected.defaultModel ?? 'auto'})</option>
                  {selected.models.map(m => <option key={m} value={m}>{m}</option>)}
                </select>
              ) : (
                <input
                  className="set-input"
                  value={model}
                  onChange={e => setModel(e.target.value)}
                  placeholder={selected.defaultModel ?? 'model name…'}
                />
              )}
            </SettingsField>

            {selected.requiresApiKey && (
              <SettingsField label="API Key">
                <SecretInput
                  value={apiKey}
                  onChange={setApiKey}
                  placeholder={current?.hasApiKey ? 'Already applied' : 'Paste API key…'}
                />
              </SettingsField>
            )}

            {selected.requiresEndpoint && (
              <SettingsField label="Endpoint URL">
                <input
                  className="set-input"
                  value={endpoint}
                  onChange={e => setEndpoint(e.target.value)}
                  placeholder="http://localhost:11434"
                />
              </SettingsField>
            )}
          </>
        )}

        {provider && (
          <SaveButton status={llmStatus} onClick={handleLlmSave}>Apply</SaveButton>
        )}
      </SettingsGroup>

      {window.electronAPI && (
        <SettingsGroup
          title="Startup Injection"
          description="Keys saved here are injected as environment variables when the local backend starts, taking effect on the next app restart. Applying the provider above also saves here."
        >
          {providers.length > 0
            ? providers.map(p => <ElectronProviderKey key={p.type} provider={p} />)
            : <p className="set-hint">No providers reported by the backend yet.</p>}
        </SettingsGroup>
      )}

      <SettingsGroup title="MCP Server" description="Expose your notegraphs to MCP clients such as Claude Desktop, Claude Code, Codex, Cursor and VS Code.">
        <SettingsToggle
          label="MCP server"
          description={mcp?.isEnabled ? 'Enabled — accepting connections' : 'Disabled'}
          checked={!!mcp?.isEnabled}
          onChange={handleMcpToggle}
          disabled={mcpToggling || !mcp}
        />

        {mcp && !mcp.isUserConfigured && (
          <SettingsField hint="Without this, MCP returns every user's graphs. Scope it to your account only.">
            <button className="btn-ghost set-self-start" onClick={handleMcpConfigureUser} disabled={mcpConfiguring}>
              {mcpConfiguring ? 'Configuring…' : 'Configure for my account'}
            </button>
          </SettingsField>
        )}

        {mcp?.isEnabled && mcpClient && (
          <>
            <SettingsField label="Client">
              <select
                className="set-input"
                value={mcpClient.id}
                onChange={e => { setMcpClientId(e.target.value); setMcpCopied(false) }}
              >
                {mcp.clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </SettingsField>

            <SettingsField label={`${mcpClient.name} setup`} hint={withInlineCode(mcpClient.instructions)}>
              <div className="set-code-block">
                <pre className="set-code">{mcpClient.config}</pre>
                <button className="btn-ghost set-copy-btn" onClick={handleMcpCopy}>
                  {mcpCopied ? '✓ Copied' : 'Copy'}
                </button>
              </div>
            </SettingsField>
          </>
        )}

        {mcp && (
          <SettingsField>
            {!mcpRegenConfirm ? (
              <button className="btn-ghost set-self-start" onClick={() => setMcpRegenConfirm(true)}>
                Regenerate secret key
              </button>
            ) : (
              <div className="set-confirm">
                <p className="set-error">This will disconnect all existing MCP clients. Continue?</p>
                <div className="set-row">
                  <button className="btn-danger" onClick={handleMcpRegenerate} disabled={mcpRegenerating}>
                    {mcpRegenerating ? 'Regenerating…' : 'Yes, regenerate'}
                  </button>
                  <button className="btn-ghost" onClick={() => setMcpRegenConfirm(false)} disabled={mcpRegenerating}>
                    Cancel
                  </button>
                </div>
              </div>
            )}
          </SettingsField>
        )}
      </SettingsGroup>
    </>
  )
}
