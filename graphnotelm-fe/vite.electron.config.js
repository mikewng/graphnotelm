import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Separate Vite config for Electron builds.
// Key differences from the web build:
//   - base: './'  so all asset paths are relative (required for file:// protocol)
//   - outDir points into desktop-executable/frontend/dist
//   - VITE_API_URL is hardcoded to the local backend port
export default defineConfig({
  plugins: [react()],
  base: './',
  build: {
    outDir: 'desktop-executable/frontend/dist',
    emptyOutDir: true,
  },
  define: {
    'import.meta.env.VITE_API_URL': JSON.stringify('http://localhost:5000'),
  },
})
