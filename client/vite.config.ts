import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In dev mode /api requests are forwarded to the C# server (override with the API_URL env variable).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': process.env.API_URL ?? 'http://localhost:5080',
    },
  },
})
