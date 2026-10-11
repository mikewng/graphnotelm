const { app, BrowserWindow, dialog, ipcMain, Menu, nativeTheme } = require('electron')
const { spawn } = require('child_process')
const path = require('path')
const net = require('net')
const fs = require('fs')

// ── Persisted user config (stored outside the repo in Electron's userData) ───
function configPath() {
  return path.join(app.getPath('userData'), 'config.json')
}

function readConfig() {
  try { return JSON.parse(fs.readFileSync(configPath(), 'utf8')) } catch { return {} }
}

function writeConfig(data) {
  const merged = { ...readConfig(), ...data }
  fs.writeFileSync(configPath(), JSON.stringify(merged, null, 2), 'utf8')
}

ipcMain.handle('load-config', ()     => readConfig())
ipcMain.handle('save-config', (_, d) => writeConfig(d))

// ── Window controls ───────────────────────────────────────────────────────────
ipcMain.handle('window-minimize',    () => mainWindow?.minimize())
ipcMain.handle('window-maximize',    () => {
  if (mainWindow?.isMaximized()) mainWindow.unmaximize()
  else mainWindow?.maximize()
})
ipcMain.handle('window-close',       () => mainWindow?.close())
ipcMain.handle('window-is-maximized',() => mainWindow?.isMaximized() ?? false)

// Allow self-signed certs from localhost (backend dev certificate)
app.commandLine.appendSwitch('ignore-certificate-errors')

const BACKEND_PORT = 5000

let backendProcess = null
let mainWindow = null

// ── Path helpers ──────────────────────────────────────────────────────────────
// When packaged, extraResources land in process.resourcesPath (outside the asar)
function backendPath() {
  return app.isPackaged
    ? path.join(process.resourcesPath, 'backend', 'graphnotelm.exe')
    : path.join(__dirname, 'backend', 'graphnotelm.exe')
}

function frontendPath() {
  return path.join(__dirname, 'frontend', 'dist', 'index.html')
}

// ── Backend ───────────────────────────────────────────────────────────────────
function spawnBackend() {
  const exe = backendPath()
  console.log('[backend] path:', exe)
  console.log('[backend] exists:', fs.existsSync(exe))

  if (!fs.existsSync(exe)) {
    dialog.showErrorBox('Backend not found', `Could not find:\n${exe}`)
    app.quit()
    return
  }

  const config = readConfig()
  const env = { ...process.env }

  // Inject per-provider keys/endpoints stored in config.json
  // Keys are stored under config.providerKeys[type] = { apiKey?, endpoint? }
  const providerKeys = config.providerKeys || {}

  // Provider 0 — Anthropic (legacy single key also supported)
  const anthropicKey = providerKeys['0']?.apiKey || config.anthropicKey
  if (anthropicKey) {
    env.ANTHROPIC_API_KEY = anthropicKey
    env.Anthropic__ApiKey = anthropicKey
    console.log('[backend] Anthropic key injected')
  }

  // Provider 1 — OpenAI
  if (providerKeys['1']?.apiKey) {
    env.OPENAI_API_KEY = providerKeys['1'].apiKey
    env.OpenAI__ApiKey = providerKeys['1'].apiKey
    console.log('[backend] OpenAI key injected')
  }

  // Provider 2 — Ollama (endpoint, no key required)
  if (providerKeys['2']?.endpoint) {
    env.OLLAMA_ENDPOINT   = providerKeys['2'].endpoint
    env.Ollama__Endpoint  = providerKeys['2'].endpoint
    console.log('[backend] Ollama endpoint injected')
  }

  // Use userData as the working directory so relative paths in appsettings.json
  // (e.g. data/notegraphs.json) always resolve to the same persistent location
  const dataDir = app.getPath('userData')
  fs.mkdirSync(dataDir, { recursive: true })

  backendProcess = spawn(exe, ['--urls', `http://localhost:${BACKEND_PORT}`], {
    env,
    cwd: dataDir,
    stdio: ['ignore', 'pipe', 'pipe'],
    windowsHide: true,
  })

  backendProcess.stdout.on('data', d => console.log('[backend]', d.toString().trim()))
  backendProcess.stderr.on('data', d => console.error('[backend]', d.toString().trim()))
  backendProcess.on('error', err => console.error('[backend] spawn error:', err.message))
  backendProcess.on('exit', (code, signal) =>
    console.log(`[backend] exited — code ${code}, signal ${signal}`)
  )
}

// TCP-level ping — works even if the backend has no unauthenticated HTTP routes
function tcpPing() {
  return new Promise((resolve, reject) => {
    const socket = new net.Socket()
    socket.setTimeout(800)
    socket.connect(BACKEND_PORT, '127.0.0.1', () => { socket.destroy(); resolve() })
    socket.on('error', err => { socket.destroy(); reject(err) })
    socket.on('timeout',   () => { socket.destroy(); reject(new Error('timeout')) })
  })
}

// A refused connect fails within a few ms, so a short interval is cheap and adds at most
// 50 ms after the backend starts listening.
async function waitForBackend(timeoutMs = 30_000) {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    try { await tcpPing(); return } catch { /* still starting */ }
    await new Promise(r => setTimeout(r, 50))
  }
  throw new Error(`Backend did not respond on port ${BACKEND_PORT} within ${timeoutMs / 1000}s`)
}

// The window loads the UI while the backend is still starting, and the renderer waits on this
// before its first request. It resolves once the backend is up, or once the user chooses to
// continue without it.
let resolveBackendReady
const backendReady = new Promise(resolve => { resolveBackendReady = resolve })
ipcMain.handle('backend-ready', () => backendReady)

// ── Window ────────────────────────────────────────────────────────────────────
function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1400,
    height: 900,
    minWidth: 960,
    minHeight: 600,
    show: false,
    frame: false,
    title: 'GraphNoteLM',
    backgroundColor: '#1e1b17',
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      preload: path.join(__dirname, 'preload.js'),
    },
  })

  mainWindow.loadFile(frontendPath())
  mainWindow.once('ready-to-show', () => mainWindow.show())
  mainWindow.on('closed', () => { mainWindow = null })

  // Notify renderer when maximize state changes
  mainWindow.on('maximize',   () => mainWindow.webContents.send('window-maximize-change', true))
  mainWindow.on('unmaximize', () => mainWindow.webContents.send('window-maximize-change', false))

  return mainWindow
}

// ── Main ──────────────────────────────────────────────────────────────────────
async function main() {
  await app.whenReady()

  // Dark title bar + remove File/Edit/View menu
  nativeTheme.themeSource = 'dark'
  Menu.setApplicationMenu(null)

  const win = createWindow()

  spawnBackend()

  try {
    await waitForBackend()
  } catch (err) {
    console.error('[startup]', err.message)
    const { response } = await dialog.showMessageBox(win, {
      type: 'warning',
      title: 'Backend not responding',
      message: err.message,
      detail: 'The app may not work correctly. Continue anyway?',
      buttons: ['Continue', 'Quit'],
      defaultId: 0,
    })
    if (response === 1) { app.quit(); return }
  }
  resolveBackendReady()
}

main()

// ── Cleanup ───────────────────────────────────────────────────────────────────
app.on('before-quit', () => {
  if (backendProcess) {
    backendProcess.kill()
    backendProcess = null
  }
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})

app.on('activate', () => {
  if (!mainWindow) createWindow()
})
