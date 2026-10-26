import { defineConfig } from '@playwright/test';
import ddsConfig from './playwright.dds.config.ts';

export default defineConfig({
  ...ddsConfig,
  testMatch: 'offline-listas.spec.ts',
  use: { ...ddsConfig.use, channel: process.env.PLAYWRIGHT_CHANNEL },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
