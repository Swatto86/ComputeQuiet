import { defineConfig } from "vite";

// Vanilla TypeScript, no framework: four panels over a Rust engine.
export default defineConfig({
  root: "ui",
  build: { outDir: "dist", emptyOutDir: true, target: "esnext" },
  server: { port: 5173, strictPort: true },
  clearScreen: false,
});
