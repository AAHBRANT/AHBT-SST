import { defineConfig } from '@playwright/test';
import base from './playwright.dds.config.ts';
export default defineConfig({ ...base, testMatch: 'uniforme-visual.spec.ts',
  use: { ...base.use, channel: process.env.PLAYWRIGHT_CHANNEL },
  projects: [{ name: 'chromium', use: { browserName: 'chromium' } }],
});
