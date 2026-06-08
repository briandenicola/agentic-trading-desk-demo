import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';

// The repo is reached through a Windows junction (C:\...\Code\WF-Garage ->
// OneDrive real path). Pin `root` to this config file's own resolved directory so
// Vite's index.html path stays consistent with the realpath and the build emits
// correctly regardless of which path the build is launched from.
const projectRoot = fileURLToPath(new URL('.', import.meta.url));

// Dev server proxies /api/* to the orchestration API so the cockpit is mode-blind.
// In production the same proxy is performed by nginx (see nginx.conf).
export default defineConfig({
  root: projectRoot,
  plugins: [react()],
  resolve: {
    preserveSymlinks: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.ORCHESTRATION_API_URL ?? 'http://localhost:8081',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    sourcemap: true,
  },
});
