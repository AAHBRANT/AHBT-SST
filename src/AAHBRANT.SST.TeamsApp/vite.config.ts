import fs from 'node:fs'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

// Sideload no Teams exige HTTPS na contentUrl da tab — reaproveita o certificado de
// desenvolvimento do ASP.NET Core (já confiável na máquina via `dotnet dev-certs https --trust`)
// em vez de gerar um certificado próprio ou depender de um dev tunnel externo. Só ativa quando as
// variáveis de ambiente existem, então o `npm run dev` normal (sem Teams) continua em HTTP puro.
const certificadoDev =
  process.env.VITE_DEV_CERT_PATH && process.env.VITE_DEV_CERT_PASSWORD
    ? { pfx: fs.readFileSync(process.env.VITE_DEV_CERT_PATH), passphrase: process.env.VITE_DEV_CERT_PASSWORD }
    : undefined

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    // Service worker que cacheia o "casco" do app (JS/CSS/HTML) — sem isso, o app nem abre sem
    // internet, mesmo com os dados dos módulos-piloto já em IndexedDB (ver src/lib/offline).
    // Só cobre uso standalone/PWA fora do Teams: dentro do Teams o app roda num iframe do
    // cliente Teams, que pode ter suas próprias regras de cache/offline fora do nosso controle.
    VitePWA({
      registerType: 'autoUpdate',
      manifest: {
        name: 'AAHBRANT SST',
        short_name: 'AAHBRANT SST',
        description: 'Gestão de Saúde e Segurança do Trabalho',
        theme_color: '#670000',
        background_color: '#ffffff',
        display: 'standalone',
        icons: [{ src: '/favicon.svg', sizes: 'any', type: 'image/svg+xml' }],
      },
      workbox: {
        // Chamadas de API não entram no cache do service worker — quem decide o que fica
        // disponível offline é o motor de sincronização (src/lib/offline), que sabe distinguir
        // GET cacheável de mutação enfileirada. O SW aqui só garante que o app abre.
        navigateFallbackDenylist: [/^\/api\//],
      },
    }),
  ],
  server: {
    port: process.env.PORT ? Number(process.env.PORT) : 5173,
    https: certificadoDev,
  },
})
