import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true, // falla si el puerto está ocupado, en vez de buscar otro
    proxy: {
      // Redirige las llamadas a /api al backend .NET en desarrollo
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
      '/swagger': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
  },
})
