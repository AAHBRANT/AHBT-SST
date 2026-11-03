import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';

const DURACAO_MS = 2500;

// Confirmação em tela cheia (opção B aprovada em 03/11): o trabalhador vê de longe, no quiosque, que a
// assinatura dele entrou. Dispara toda vez que `nome` passa de vazio para preenchido — as telas já limpam o
// último assinante antes de cada captura, então a mesma pessoa duas vezes seguidas também mostra de novo.
export function AssinaturaConfirmadaTelaCheia({ nome }: { nome: string | null }) {
  const [visivel, setVisivel] = useState<{ nome: string; hora: string } | null>(null);

  useEffect(() => {
    if (!nome) return;
    setVisivel({ nome, hora: new Date().toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) });
    const t = window.setTimeout(() => setVisivel(null), DURACAO_MS);
    return () => window.clearTimeout(t);
  }, [nome]);

  if (!visivel) return null;

  return createPortal(
    <div
      role="status"
      aria-live="assertive"
      onClick={() => setVisivel(null)}
      className="assinatura-ok"
    >
      <style>{CSS}</style>
      <div className="assinatura-ok__icone">
        <div className="assinatura-ok__anel" />
        <div className="assinatura-ok__disco">
          <svg width="76" height="76" viewBox="0 0 24 24" aria-hidden="true">
            <path className="assinatura-ok__check" d="M5 12.5l4.5 4.5L19 7.5" />
          </svg>
        </div>
      </div>
      <div className="assinatura-ok__texto">
        <div className="assinatura-ok__nome">{visivel.nome}</div>
        <div className="assinatura-ok__sub">Assinatura registrada · {visivel.hora}</div>
      </div>
    </div>,
    document.body,
  );
}

const CSS = `
.assinatura-ok{position:fixed;inset:0;z-index:2000000;background:#15803d;color:#fff;display:flex;flex-direction:column;
  align-items:center;justify-content:center;gap:24px;padding:24px;text-align:center;cursor:pointer;
  font-family:'Segoe UI','Segoe UI Web (West European)',-apple-system,BlinkMacSystemFont,Roboto,'Helvetica Neue',sans-serif;
  animation:assinaturaOkFundo 2.5s ease-in-out both}
.assinatura-ok__icone{position:relative;width:150px;height:150px}
.assinatura-ok__anel{position:absolute;inset:0;border-radius:50%;border:4px solid #fff;animation:assinaturaOkAnel 1.2s ease-out .35s both}
.assinatura-ok__disco{position:absolute;inset:0;border-radius:50%;background:#fff;display:flex;align-items:center;justify-content:center;
  animation:assinaturaOkPop .55s cubic-bezier(.2,.9,.3,1.2) both}
.assinatura-ok__check{fill:none;stroke:#15803d;stroke-width:2.6;stroke-linecap:round;stroke-linejoin:round;
  stroke-dasharray:48;animation:assinaturaOkDesenho .9s ease-out both}
.assinatura-ok__texto{display:flex;flex-direction:column;gap:8px;animation:assinaturaOkSobe .9s ease-out both}
.assinatura-ok__nome{font-size:clamp(26px,5vw,44px);font-weight:700;line-height:1.15;overflow-wrap:anywhere}
.assinatura-ok__sub{font-size:clamp(15px,2.2vw,20px);opacity:.95}
@keyframes assinaturaOkFundo{0%{opacity:0}8%,88%{opacity:1}100%{opacity:0}}
@keyframes assinaturaOkPop{0%{transform:scale(.4);opacity:0}55%{transform:scale(1.08);opacity:1}75%{transform:scale(.97)}100%{transform:scale(1)}}
@keyframes assinaturaOkDesenho{0%,25%{stroke-dashoffset:48}60%,100%{stroke-dashoffset:0}}
@keyframes assinaturaOkSobe{0%,40%{transform:translateY(10px);opacity:0}70%,100%{transform:none;opacity:1}}
@keyframes assinaturaOkAnel{0%{transform:scale(1);opacity:.55}100%{transform:scale(1.7);opacity:0}}
@media (prefers-reduced-motion: reduce){.assinatura-ok,.assinatura-ok *{animation:none!important}.assinatura-ok__check{stroke-dashoffset:0}}
`;
