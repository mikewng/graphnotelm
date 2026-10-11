import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App'
// Bundled rather than fetched from Google Fonts, so startup never waits on the network.
// 700 is left out to match what was loaded before: bold text renders at 600.
import '@fontsource/inter/400.css'
import '@fontsource/inter/500.css'
import '@fontsource/inter/600.css'
import '@fontsource/jetbrains-mono/400.css'
import '@fontsource/jetbrains-mono/500.css'
import '@fontsource/jetbrains-mono/600.css'
import './app.css'

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
)
