import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

if ('serviceWorker' in navigator) {
  const recarregarQuandoServiceWorkerAtualizar = () => {
    if (sessionStorage.getItem('sst-sw-reload-em-andamento') === '1') return

    sessionStorage.setItem('sst-sw-reload-em-andamento', '1')
    window.location.reload()
  }

  navigator.serviceWorker.addEventListener('controllerchange', recarregarQuandoServiceWorkerAtualizar)

  // Pede ao navegador para comparar o sw.js do servidor com o instalado. Se houver versão nova,
  // ela instala, assume e (via public/sw-atualizacao.js) recarrega esta janela sozinha.
  const verificarAtualizacaoDoApp = () =>
    navigator.serviceWorker
      .getRegistration()
      .then((registro) => registro?.update())
      .catch(() => undefined)

  window.addEventListener('load', () => {
    verificarAtualizacaoDoApp().finally(() => {
      window.setTimeout(() => sessionStorage.removeItem('sst-sw-reload-em-andamento'), 30000)
    })

    // O Teams mantém a aba do app viva por dias sem nunca navegar de novo, então o check de
    // atualização que o navegador faz sozinho (só a cada 24h, ou em navegação) não basta.
    // Checa periodicamente e sempre que o usuário volta para a aba do app.
    const QUINZE_MINUTOS = 15 * 60 * 1000
    window.setInterval(() => void verificarAtualizacaoDoApp(), QUINZE_MINUTOS)
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') void verificarAtualizacaoDoApp()
    })
  })
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
