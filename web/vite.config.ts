import { defineConfig } from 'vite';

export default defineConfig({
  // Relative asset paths, so the build works from any sub-path (e.g. GitHub Pages).
  base: './',
  worker: { format: 'es' },
  // three.js is most of the bundle; it's one ~150 kB gzipped chunk.
  build: { target: 'es2022', chunkSizeWarningLimit: 800 },
});
