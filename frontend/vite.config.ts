import { fileURLToPath, URL } from 'node:url'
import { readFileSync } from 'node:fs'
import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const target = env.DEV_API_TARGET
  if (target && new URL(target).protocol !== 'https:') throw new Error('DEV_API_TARGET must use HTTPS.')
  if (Boolean(env.DEV_HTTPS_CERT) !== Boolean(env.DEV_HTTPS_KEY)) {
    throw new Error('Set both DEV_HTTPS_CERT and DEV_HTTPS_KEY.')
  }
  return {
    plugins: [vue(), vueDevTools()],
    resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
    server: {
      ...(env.DEV_HTTPS_CERT && env.DEV_HTTPS_KEY ? {
        https: { cert: readFileSync(env.DEV_HTTPS_CERT), key: readFileSync(env.DEV_HTTPS_KEY) },
      } : {}),
      ...(target ? { proxy: { '/api': { target, changeOrigin: true, secure: true } } } : {}),
    },
  }
})
