import { defineConfig } from '@playwright/test';

// API e SQL Server isolados: informe SST_TEST_API e inicie a API de teste antes da suíte.
// Nunca apontar esta suíte para homologação/produção: ela cadastra obras e temas de teste.
export default defineConfig({
  testDir: './tests-ui', testMatch: 'cronograma-dds.spec.ts', workers: 1,
  timeout: 60_000, expect: { timeout: 15_000 },
  use: { baseURL: 'http://localhost:5199', timezoneId: 'America/Sao_Paulo', viewport: { width: 1440, height: 1080 }, reducedMotion: 'reduce' },
  webServer: {
    command: 'npm run dev -- --host localhost --port 5199 --strictPort',
    url: 'http://localhost:5199', reuseExistingServer: !process.env.CI,
    env: { VITE_API_BASE_URL: process.env.SST_TEST_API ?? 'http://localhost:5198', VITE_ENTRA_TENANT_ID: '' },
  },
});
