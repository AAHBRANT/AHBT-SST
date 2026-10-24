import { defineConfig } from '@playwright/test';
import config from './playwright.offline.config.ts';
export default defineConfig({ ...config, testMatch: 'inspecao-offline.spec.ts' });
