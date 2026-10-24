import { defineConfig } from '@playwright/test';

// Testa o build real: o Vite de desenvolvimento não instala o service worker da PWA.
export default defineConfig({
  testDir: './tests-ui',
  testMatch: 'pwa-offline.spec.ts',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  use: {
    baseURL: 'http://localhost:5189',
    viewport: { width: 800, height: 1100 },
    hasTouch: true,
    reducedMotion: 'reduce',
    channel: process.env.PLAYWRIGHT_CHANNEL,
  },
  webServer: {
    command: 'npm run preview -- --port 5189 --strictPort',
    url: 'http://localhost:5189',
    reuseExistingServer: false,
  },
});
