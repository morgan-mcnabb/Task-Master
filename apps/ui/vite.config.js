import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import { fileURLToPath, URL } from 'node:url';

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/auth':  { target: 'http://localhost:5055', changeOrigin: true },
      '/api':   { target: 'http://localhost:5055', changeOrigin: true },
      '/health':{ target: 'http://localhost:5055', changeOrigin: true },
    },
  },
  preview: {
    port: 5173,
    strictPort: true,
  },
  build: {
    outDir: '../../TaskMaster/TaskMasterApi/wwwroot',
    emptyOutDir: true,
  },
});
