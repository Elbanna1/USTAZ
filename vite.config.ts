import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'node:path'

/** Where the real API lives. Override per environment with VITE_API_TARGET. */
const DEFAULT_API_TARGET = 'https://ustaz.runasp.net'

// Point at a locally running API with:  VITE_API_TARGET=http://127.0.0.1:5195 npm run dev

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const target = env.VITE_API_TARGET?.trim() || DEFAULT_API_TARGET

  // Loud, because the fallback is production: a dev server that quietly proxies
  // to the live API will write real rows from local experiments.
  console.log(`
  API proxy -> ${target}${target === DEFAULT_API_TARGET ? '  (PRODUCTION)' : ''}
`)

  return {
    plugins: [react()],
    resolve: {
      alias: { '@': path.resolve(__dirname, './src') },
    },
    server: {
      port: 5173,
      open: false,
      /**
       * The API only answers browser calls from origins on its CORS allow-list, and a
       * dev server is never on it. Proxying keeps every request same-origin, which is
       * also how the app is meant to be deployed — behind one host.
       */
      proxy: {
        '/api': { target, changeOrigin: true, secure: true },
        '/uploads': { target, changeOrigin: true, secure: true },
      },
    },
    build: {
      rollupOptions: {
        output: {
          manualChunks: {
            vendor: ['react', 'react-dom', 'react-router-dom'],
            motion: ['framer-motion'],
            charts: ['recharts'],
          },
        },
      },
    },
  }
})
