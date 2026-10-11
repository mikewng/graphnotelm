const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('electronAPI', {
  // Config persistence
  saveConfig: (data) => ipcRenderer.invoke('save-config', data),
  loadConfig: ()     => ipcRenderer.invoke('load-config'),

  // Window controls
  minimize:    () => ipcRenderer.invoke('window-minimize'),
  maximize:    () => ipcRenderer.invoke('window-maximize'),
  close:       () => ipcRenderer.invoke('window-close'),
  isMaximized: () => ipcRenderer.invoke('window-is-maximized'),

  // Resolves once the backend accepts connections; the UI loads before it does
  whenBackendReady: () => ipcRenderer.invoke('backend-ready'),

  // Listen for maximize/unmaximize events from main process
  onMaximizeChange: (cb) => {
    ipcRenderer.on('window-maximize-change', (_, maximized) => cb(maximized))
  },
})
