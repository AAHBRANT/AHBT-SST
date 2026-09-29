import { defineConfig } from '@playwright/test';

// Porta própria para não reutilizar outro projeto aberto no Vite padrão.
export default defineConfig({
  testDir: './tests-ui',
  testMatch: 'dds-participantes.spec.ts',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  use: {
    viewport: { width: 1280, height: 900 }, baseURL: 'http://localhost:5187', reducedMotion: 'reduce',
    permissions: ['camera'],
    launchOptions: { args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream'] },
  },
  webServer: {
    command: 'npm run dev -- --port 5187 --strictPort',
    url: 'http://localhost:5187',
    reuseExistingServer: false,
    timeout: 60_000,
    env: { VITE_ENTRA_TENANT_ID: '' },
  },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
