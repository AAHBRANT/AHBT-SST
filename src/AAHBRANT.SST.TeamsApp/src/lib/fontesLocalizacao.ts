import { geoLocation } from '@microsoft/teams-js';
import { aguardarInicializacaoTeams } from '../teams/teamsInit';
import { criarRastreador, normalizarMomento, type FonteLocalizacao } from './rastreadorLocalizacao';

// Fontes reais de localização usadas pelo rastreador (ver rastreadorLocalizacao.ts). Rodam juntas:
// no iPhone o SDK do Teams costuma ser o caminho que funciona; no PC o SDK não oferece localização
// e quem responde é o WebView (serviço de localização do Windows).

const ehCelular = () => /iPhone|iPad|iPod|Android/i.test(navigator.userAgent);
const ehIos = () => /iPhone|iPad|iPod/i.test(navigator.userAgent);

export function motivoPermissao() {
  if (ehIos()) return 'Permissão de localização bloqueada. No iPhone: Ajustes › Teams › Localização › "Durante o Uso" e ative "Localização Exata".';
  if (ehCelular()) return 'Permissão de localização bloqueada. Libere a localização para o Teams nas configurações do aparelho.';
  return 'Permissão de localização bloqueada. No Windows: Configurações › Privacidade e segurança › Localização — ative e permita para o Teams. No Teams: Configurações › Permissões do app › Localização.';
}
function motivoIndisponivel() {
  return ehCelular()
    ? 'O aparelho não está conseguindo a localização. Confira se o GPS está ligado e tente perto de uma janela ou área aberta.'
    : 'O computador não está conseguindo a localização. Ative o serviço de Localização do Windows (Configurações › Privacidade e segurança › Localização).';
}

function comTeto<T>(p: Promise<T>, ms: number): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const t = setTimeout(() => reject(new Error('tempo esgotado')), ms);
    p.then(v => { clearTimeout(t); resolve(v); }, e => { clearTimeout(t); reject(e); });
  });
}
const esperar = (ms: number) => new Promise(r => setTimeout(r, ms));

// GPS do WebView em modo contínuo (watchPosition): o primeiro fix pode demorar, mas depois as
// leituras chegam sozinhas e a posição fica sempre fresca. Sem `timeout` — só para por erro real.
export const fonteNavegador: FonteLocalizacao = {
  nome: 'navegador',
  iniciar(aoLer, aoFalhar) {
    if (!navigator.geolocation) { aoFalhar('Localização indisponível neste aparelho.'); return () => {}; }
    const id = navigator.geolocation.watchPosition(
      p => aoLer({ latitude: p.coords.latitude, longitude: p.coords.longitude, precisaoMetros: p.coords.accuracy ?? null,
        obtidaEm: normalizarMomento(p.timestamp, Date.now()), fonte: 'navegador' }),
      e => aoFalhar(e.code === 1 ? motivoPermissao() : motivoIndisponivel()),
      { enableHighAccuracy: true, maximumAge: 10_000 },
    );
    return () => navigator.geolocation.clearWatch(id);
  },
};

// SDK do Teams não tem modo contínuo: repete getCurrentLocation em ciclo enquanto estiver ativo.
// Cada chamada tem teto próprio para uma resposta perdida do cliente não travar o ciclo.
export const fonteTeams: FonteLocalizacao = {
  nome: 'teams',
  iniciar(aoLer, aoFalhar) {
    let ativo = true;
    void (async () => {
      try {
        if (!(await aguardarInicializacaoTeams()) || !geoLocation.isSupported()) { aoFalhar('Teams sem localização neste dispositivo.'); return; }
        const permitido = await comTeto(geoLocation.hasPermission(), 10_000).catch(() => false)
          || await comTeto(geoLocation.requestPermission(), 30_000).catch(() => false);
        if (!ativo) return;
        if (!permitido) { aoFalhar(motivoPermissao()); return; }
        let falhasSeguidas = 0;
        while (ativo) {
          try {
            const pos = await comTeto(geoLocation.getCurrentLocation(), 15_000);
            if (!ativo) return;
            falhasSeguidas = 0;
            aoLer({ latitude: pos.latitude, longitude: pos.longitude, precisaoMetros: pos.accuracy ?? null,
              obtidaEm: normalizarMomento(pos.timestamp, Date.now()), fonte: 'teams' });
            await esperar(5_000);
          } catch {
            if (!ativo) return;
            if (++falhasSeguidas === 3) aoFalhar(motivoIndisponivel());
            await esperar(2_000);
          }
        }
      } catch {
        if (ativo) aoFalhar('O Teams não conseguiu iniciar a localização.');
      }
    })();
    return () => { ativo = false; };
  },
};

export const criarRastreadorFoto = () => criarRastreador({ fontes: [fonteTeams, fonteNavegador] });
