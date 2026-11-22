import react from '@vitejs/plugin-react'
import fs from 'node:fs'
import path from 'node:path'
import { defineConfig, type Plugin } from 'vite'

/**
 * Card pictures: every image in public/cards (e.g. STRIKER.png) is listed in /cards/manifest.json,
 * so the game knows which cards have a picture. Just drop files in the folder — no code changes needed.
 */
function cardManifest(): Plugin {
  const dir = path.resolve(__dirname, 'public/cards')
  const list = () => {
    const out: Record<string, string> = {}
    if (!fs.existsSync(dir)) return out
    for (const f of fs.readdirSync(dir)) {
      const m = /^([A-Z0-9_]+)\.(png|webp|jpe?g|avif)$/i.exec(f)
      if (m) out[m[1].toUpperCase()] = `/cards/${f}`
    }
    return out
  }
  return {
    name: 'card-manifest',
    configureServer(server) {
      server.middlewares.use('/cards/manifest.json', (_req, res) => {
        res.setHeader('Content-Type', 'application/json')
        res.end(JSON.stringify(list()))
      })
    },
    generateBundle() {
      this.emitFile({ type: 'asset', fileName: 'cards/manifest.json', source: JSON.stringify(list()) })
    },
  }
}

// In dev mode /api requests are forwarded to the C# server (override with the API_URL env variable).
export default defineConfig({
  plugins: [react(), cardManifest()],
  server: {
    port: 5173,
    proxy: {
      '/api': process.env.API_URL ?? 'http://localhost:5080',
    },
  },
})
