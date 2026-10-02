import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,           // distinto al 5173 de Compas.Api para poder correr los dos a la vez
    strictPort: true,
    proxy: {
      // Redirige /api al backend Compas.Api
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
  },
})
