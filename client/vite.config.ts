import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// В режим на разработка заявките към /api се препращат към C# сървъра.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
})
