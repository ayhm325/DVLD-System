import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [tailwindcss(), react()],

  resolve: {
    alias: {
      "@": path.resolve(import.meta.dirname, "./src"),
    },
  },

  server: {
    host: "0.0.0.0",
    port: 5173,
    strictPort: true,
    // Development-server proxy: the browser stays same-origin with Vite while
    // the API remains bound to localhost on the development machine.
    proxy: {
      "/api": {
        target: "https://localhost:7125",
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
