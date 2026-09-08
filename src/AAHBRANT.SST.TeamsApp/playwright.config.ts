import { defineConfig } from '@playwright/test';

// Snapshots visuais da galeria ui/ (spec §6). Sobe o Vite, abre /#/ui-galeria nos dois temas e compara
// cada <section> com a referência commitada. Falhou = mudança visual: intencional (--update-snapshots)
// ou regressão.
export default defineConfig({
  testDir: './tests-ui',
  timeout: 60_000,
  expect: { toHaveScreenshot: { maxDiffPixelRatio: 0.002, animations: 'disabled' } },
  use: { viewport: { width: 1280, height: 900 }, baseURL: 'http://localhost:5173', reducedMotion: 'reduce' },
  webServer: { command: 'npm run dev -- --port 5173', url: 'http://localhost:5173', reuseExistingServer: true, timeout: 60_000 },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
