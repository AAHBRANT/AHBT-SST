# Câmeras — detecção de EPI (frontend) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Telas do módulo Câmeras no app do Teams: item próprio na barra lateral (visível só para o usuário do piloto), mosaico de câmeras, tela Monitorar (câmera do tablet com detector de movimento) e aba Alertas de EPI.

**Architecture:** Página `CamerasPage` com duas abas (`Câmeras` = mosaico/lista com cadastro; `Alertas de EPI` = ocorrências) e uma página `MonitorarCameraPage` por câmera. A lógica de captura fica em um hook (`useMonitoramentoCamera`) sobre uma função pura (`detectorMovimento`). Toda a comunicação passa por `api.cameras` em `src/lib/api.ts`. O backend já existe (branch `feature/cameras-epi`).

**Tech Stack:** React 19, TypeScript, Fluent UI via a camada `@ui`, TanStack Query (`src/lib/consultas.ts`), react-router 7, Vite. Verificação por `npm run build` (`tsc -b && vite build`) e `npm run lint` (oxlint). O projeto não tem runner de teste unitário no frontend.

**Spec:** `docs/superpowers/specs/2026-11-09-cameras-epi-design.md`
**Mockup aprovado (contrato visual):** `docs/superpowers/mockups/2026-11-09-cameras/` — `Main.dc.html` (mosaico), `Monitorar.dc.html`, `Ocorrencias.dc.html`. Canvas publicado: https://claude.ai/artifact/R3y6sz1p2KFtKGE6ntZXdn
**Contrato da API:** seção "Contrato da API" de `docs/superpowers/plans/2026-11-09-cameras-epi-backend.md`, mais `GET /api/cameras/acesso` → `{ permitido: boolean }` e o campo `totalAnalises` em `CameraDto`.

## Global Constraints

- Trabalhar na worktree `C:\Projetos\SST-APP-cameras`, branch `feature/cameras-epi`. Não usar `C:\Projetos\SST-APP`.
- Pasta do app: `src/AAHBRANT.SST.TeamsApp`. Páginas importam componentes de `@ui`, nunca de `@fluentui/react-components` direto (ícones de `@fluentui/react-icons` são permitidos). Cores só por tokens (`designTokens`, `tokensUi`), sem hex solto.
- O visual segue o mockup aprovado: item **Câmeras** na barra lateral logo abaixo de **Operação**; abas **Câmeras** e **Alertas de EPI** (não "Ocorrências", que já é outro módulo); tela 1 em **mosaico** com alternância mosaico/lista e botão **Tela cheia**.
- O item do menu e as rotas só existem para quem `GET /api/cameras/acesso` devolve `permitido: true`. Para os demais, o item não aparece e a rota redireciona para `/`.
- EPIs: somente capacete e colete. Nada de luvas.
- Análise só quando há movimento; no máximo um quadro enviado a cada **30 s** por câmera (o servidor ignora abaixo de 25 s).
- Foto enviada em JPEG, lado maior ≤ 1280 px, qualidade 0,8 (fica bem abaixo do limite de 4 MB do servidor).
- Estados do bloco no mosaico derivam dos dados reais: **Monitorando** (câmera ligada e última análise há menos de 2 min), **Sem análise recente** (ligada, sem análise nos últimos 2 min — substitui o "Sem sinal" do mockup, que só fará sentido com câmera fixa), **Desligada**. Não há imagem ao vivo no mosaico nesta fase: só a tela Monitorar mostra o vídeo, do próprio dispositivo.
- Textos em português, enquadramento positivo, sem a palavra "vencido". Todo estado indicado por cor também tem texto.
- Não adicionar dependência nova ao `package.json`. Não alterar outros módulos além dos pontos listados.
- **Sem entrada no `NovidadesSeeder` nesta fase:** o módulo só é visível para um usuário; anunciar a todos um item que não aparece seria enganoso. A entrada entra quando o piloto for aberto.
- Não fazer push, PR nem deploy. Não aplicar migration nem resetar banco sem autorização do usuário.
- Commits terminam com `Co-Authored-By: Claude <noreply@anthropic.com>`.
- Verificação de cada tarefa: `npm run build` e `npm run lint` dentro de `src/AAHBRANT.SST.TeamsApp`, ambos sem erros novos.

## File Structure

| Arquivo (sob `src/AAHBRANT.SST.TeamsApp/src`) | Responsabilidade |
| --- | --- |
| `lib/api.ts` (modificar) | Tipos e `api.cameras` |
| `lib/consultas.ts` (modificar) | Chaves e hooks de consulta do módulo |
| `pages/cameras/detectorMovimento.ts` | Função pura: fração de pixels alterados entre dois quadros |
| `pages/cameras/useMonitoramentoCamera.ts` | Câmera do dispositivo, laço de amostragem, envio de quadros |
| `pages/cameras/MonitorarCameraPage.tsx` | Tela Monitorar |
| `pages/cameras/CamerasPage.tsx` | Cabeçalho + abas |
| `pages/cameras/MosaicoCamerasTab.tsx` | Mosaico/lista, filtro por obra, tela cheia |
| `pages/cameras/FormularioCamera.tsx` | Criar/editar câmera |
| `pages/cameras/AlertasEpiTab.tsx` | Lista de ocorrências, vínculo e falso alerta |
| `pages/cameras/FotoOcorrenciaEpi.tsx` | Carrega a foto protegida como object URL |
| `pages/cameras/rotulos.ts` | Rótulos e tons de tipo, situação e estado |
| `layout/AppShell.tsx` (modificar) | Item "Câmeras" condicional |
| `App.tsx` (modificar) | Rotas `/cameras` e `/cameras/:id/monitorar` com guarda de acesso |

---

### Task 1: Cliente da API e consultas

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/api.ts`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/lib/consultas.ts`

**Interfaces:**
- Produces (exportados de `lib/api.ts`): `TipoCamera`, `SituacaoOcorrenciaEpi`, `CameraResumo`, `DadosCamera`, `ResultadoQuadro`, `OcorrenciaEpi`, `FiltroOcorrenciasEpi`, e `api.cameras` com `acesso()`, `listar(obraId?)`, `criar(dados)`, `atualizar(id, dados)`, `excluir(id)`, `analisarQuadro(id, foto, capturadaEm)`, `listarOcorrencias(filtro)`, `baixarFotoOcorrencia(id)`, `vincularTrabalhador(id, trabalhadorId)`, `definirSituacao(id, situacao)`.
- Produces (de `lib/consultas.ts`): `useAcessoCameras()`, `useCameras(obraId?)`, `useOcorrenciasEpi(filtro)`.

- [ ] **Step 1: Ler os padrões antes de escrever**

Em `lib/api.ts`: a função `request<T>` (≈ linha 4625), um método que envia `FormData` com cabeçalhos de autenticação (procurar `new FormData()` dentro de `export const api`, ex.: `obras.criar`) e um que devolve `Blob` (procurar `response.blob()`), o bloco `veiculos:` (modelo de CRUD) e as funções `ehRotaOffline` / `ehLeituraDeApoioOffline`. Confirmar que os caminhos `/api/cameras...` **não** casam com as rotas offline; se casarem por prefixo genérico, parar e reportar. Em `lib/consultas.ts`: como `chaves` e `useLista` são definidos (ex.: `useObras`).

- [ ] **Step 2: Tipos**

Acrescentar em `lib/api.ts`, junto dos tipos de veículo:

```ts
export const TipoCamera = { Canteiro: 1, Portaria: 2 } as const;
export const tipoCameraLabel: Record<number, string> = { 1: 'Canteiro', 2: 'Portaria' };

export const SituacaoOcorrenciaEpi = { Nova: 1, Conferida: 2, FalsoAlerta: 3 } as const;
export const situacaoOcorrenciaEpiLabel: Record<number, string> = { 1: 'Nova', 2: 'Conferida', 3: 'Falso alerta' };

export interface CameraResumo {
  id: string;
  obraId: string;
  obraNome: string;
  nome: string;
  areaSstId: string | null;
  areaNome: string | null;
  tipo: number;
  habilitada: boolean;
  intervaloMinimoAlertasMinutos: number;
  ultimaAnaliseEmUtc: string | null;
  ocorrenciasHoje: number;
  totalAnalises: number;
}

export interface DadosCamera {
  obraId: string;
  nome: string;
  areaSstId: string | null;
  tipo: number;
  habilitada: boolean;
  intervaloMinimoAlertasMinutos: number;
}

export type SituacaoQuadro = 'ignorado' | 'limpo' | 'ocorrencia' | 'falha';

export interface ResultadoQuadro {
  situacao: SituacaoQuadro;
  ocorrenciaId: string | null;
  alertaEnviado: boolean;
  pessoas: number;
  semCapacete: number;
  semColete: number;
}

export interface OcorrenciaEpi {
  id: string;
  cameraId: string;
  cameraNome: string;
  obraId: string;
  obraNome: string;
  capturadaEmUtc: string;
  recebidaEmUtc: string;
  pessoasNaCena: number;
  semCapacete: number;
  semColete: number;
  confianca: number;
  alertaEnviado: boolean;
  trabalhadorId: string | null;
  trabalhadorNome: string | null;
  situacao: number;
}

export interface FiltroOcorrenciasEpi {
  obraId?: string;
  cameraId?: string;
  de?: string;
  ate?: string;
  situacao?: number;
}
```

- [ ] **Step 3: `api.cameras`**

Dentro de `export const api = { ... }`, logo após o bloco `veiculos`. Os dois métodos que não usam `request` (multipart e blob) devem copiar **exatamente** a forma como os métodos existentes de `FormData`/`blob` montam URL e cabeçalhos (`API_BASE_URL`, `montarHeadersAuth()`, tratamento de erro com `extrairMensagemErro`); o esqueleto abaixo mostra a intenção:

```ts
  cameras: {
    acesso: () => request<{ permitido: boolean }>('/api/cameras/acesso'),
    listar: (obraId?: string) =>
      request<CameraResumo[]>(`/api/cameras${obraId ? `?obraId=${obraId}` : ''}`),
    criar: (dados: DadosCamera) =>
      request<{ id: string }>('/api/cameras', { method: 'POST', body: JSON.stringify(dados) }),
    atualizar: (id: string, dados: Omit<DadosCamera, 'obraId'>) =>
      request<void>(`/api/cameras/${id}`, { method: 'PUT', body: JSON.stringify({ id, ...dados }) }),
    excluir: (id: string) => request<void>(`/api/cameras/${id}`, { method: 'DELETE' }),

    // multipart: campos "Foto" (JPEG) e "CapturadaEm" (ISO 8601). Sem Content-Type manual:
    // o navegador define o boundary.
    analisarQuadro: async (id: string, foto: Blob, capturadaEm: Date): Promise<ResultadoQuadro> => {
      const formData = new FormData();
      formData.append('Foto', foto, 'quadro.jpg');
      formData.append('CapturadaEm', capturadaEm.toISOString());
      const response = await fetch(`${API_BASE_URL}/api/cameras/${id}/quadros`, {
        method: 'POST',
        headers: await montarHeadersAuth(),
        body: formData,
      });
      if (!response.ok) {
        const corpo = await response.text().catch(() => '');
        throw new Error(extrairMensagemErro(corpo, response.status, response.statusText));
      }
      return (await response.json()) as ResultadoQuadro;
    },

    listarOcorrencias: (filtro: FiltroOcorrenciasEpi) => {
      const qs = new URLSearchParams();
      if (filtro.obraId) qs.set('obraId', filtro.obraId);
      if (filtro.cameraId) qs.set('cameraId', filtro.cameraId);
      if (filtro.de) qs.set('de', filtro.de);
      if (filtro.ate) qs.set('ate', filtro.ate);
      if (filtro.situacao) qs.set('situacao', String(filtro.situacao));
      const s = qs.toString();
      return request<OcorrenciaEpi[]>(`/api/cameras/ocorrencias${s ? `?${s}` : ''}`);
    },

    baixarFotoOcorrencia: async (id: string): Promise<Blob> => {
      const response = await fetch(`${API_BASE_URL}/api/cameras/ocorrencias/${id}/foto`, {
        headers: await montarHeadersAuth(),
      });
      if (!response.ok) throw new Error('Não foi possível carregar a foto.');
      return response.blob();
    },

    vincularTrabalhador: (id: string, trabalhadorId: string | null) =>
      request<void>(`/api/cameras/ocorrencias/${id}/trabalhador`, {
        method: 'PUT',
        body: JSON.stringify({ trabalhadorId }),
      }),
    definirSituacao: (id: string, situacao: number) =>
      request<void>(`/api/cameras/ocorrencias/${id}/situacao`, {
        method: 'PUT',
        body: JSON.stringify({ situacao }),
      }),
  },
```

- [ ] **Step 4: Consultas**

Em `lib/consultas.ts`, seguindo o formato de `useObras` (mesmo helper, mesmas opções). `useAcessoCameras` não pode quebrar a barra lateral se a chamada falhar: erro conta como "sem acesso".

```ts
// chaves: acrescentar
//   camerasAcesso: ['cameras', 'acesso'] as const,
//   cameras: (obraId?: string) => ['cameras', 'lista', obraId ?? 'todas'] as const,
//   ocorrenciasEpi: (filtro: FiltroOcorrenciasEpi) => ['cameras', 'ocorrencias', filtro] as const,

export function useAcessoCameras(): boolean {
  const { data } = useQuery({
    queryKey: chaves.camerasAcesso,
    queryFn: () => api.cameras.acesso().catch(() => ({ permitido: false })),
    staleTime: 5 * 60 * 1000,
  });
  return data?.permitido === true;
}

export const useCameras = (obraId?: string) =>
  useQuery({ queryKey: chaves.cameras(obraId), queryFn: () => api.cameras.listar(obraId), refetchInterval: 30_000 });

export const useOcorrenciasEpi = (filtro: FiltroOcorrenciasEpi) =>
  useQuery({ queryKey: chaves.ocorrenciasEpi(filtro), queryFn: () => api.cameras.listarOcorrencias(filtro) });
```

Se `consultas.ts` usa um helper próprio em vez de `useQuery` direto, usar o helper e manter os nomes exportados acima.

- [ ] **Step 5: Verificar e commitar**

Run (em `src/AAHBRANT.SST.TeamsApp`): `npm run build` e `npm run lint`
Expected: sem erros.

```bash
git add src/AAHBRANT.SST.TeamsApp/src/lib/api.ts src/AAHBRANT.SST.TeamsApp/src/lib/consultas.ts
git commit -m "feat(cameras): cliente da API e consultas do módulo de câmeras"
```

---

### Task 2: Detector de movimento e hook de monitoramento

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/cameras/detectorMovimento.ts`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/cameras/useMonitoramentoCamera.ts`

**Interfaces:**
- Consumes: `api.cameras.analisarQuadro`, `ResultadoQuadro` (Task 1).
- Produces: `fracaoAlterada(anterior, atual, limiarPixel?)`, `houveMovimento(anterior, atual)`, constantes `LARGURA_AMOSTRA = 64`, `ALTURA_AMOSTRA = 36`; `useMonitoramentoCamera(cameraId)` devolvendo `{ estado, erro, videoRef, iniciar, parar, ultimaAnalise, contadores, ocorrenciasSessao, segundosParaProxima, movimento }`.

- [ ] **Step 1: Detector (função pura)**

```ts
// Compara dois quadros reduzidos em tons de cinza. Usado para só enviar foto à IA quando a cena
// muda: cena parada não gera custo. Valores definidos para o piloto; ajustar com dados reais.
export const LARGURA_AMOSTRA = 64;
export const ALTURA_AMOSTRA = 36;
export const LIMIAR_PIXEL = 25; // diferença mínima de brilho (0–255) para um pixel contar como alterado
export const LIMIAR_FRACAO = 0.02; // 2% dos pixels alterados = movimento

/** Converte RGBA (ImageData.data) em luminância, um byte por pixel. */
export function paraCinza(rgba: Uint8ClampedArray): Uint8Array {
  const cinza = new Uint8Array(rgba.length / 4);
  for (let i = 0, p = 0; i < rgba.length; i += 4, p += 1) {
    cinza[p] = (rgba[i] * 299 + rgba[i + 1] * 587 + rgba[i + 2] * 114) / 1000;
  }
  return cinza;
}

/** Fração (0–1) de pixels cuja luminância mudou mais que o limiar. Tamanhos diferentes = 1. */
export function fracaoAlterada(anterior: Uint8Array, atual: Uint8Array, limiarPixel = LIMIAR_PIXEL): number {
  if (anterior.length !== atual.length || atual.length === 0) return 1;
  let alterados = 0;
  for (let i = 0; i < atual.length; i += 1) {
    if (Math.abs(atual[i] - anterior[i]) > limiarPixel) alterados += 1;
  }
  return alterados / atual.length;
}

export function houveMovimento(anterior: Uint8Array | null, atual: Uint8Array): boolean {
  if (!anterior) return true; // primeiro quadro: analisa uma vez para ter referência
  return fracaoAlterada(anterior, atual) >= LIMIAR_FRACAO;
}
```

- [ ] **Step 2: Hook**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import { api, type ResultadoQuadro } from '../../lib/api';
import { ALTURA_AMOSTRA, LARGURA_AMOSTRA, houveMovimento, paraCinza } from './detectorMovimento';

export const INTERVALO_ENVIO_MS = 30_000; // teto do navegador; o servidor ignora abaixo de 25 s
const INTERVALO_AMOSTRA_MS = 1_000;
const LADO_MAXIMO_FOTO = 1280;
const QUALIDADE_JPEG = 0.8;

export type EstadoMonitoramento = 'parado' | 'iniciando' | 'monitorando' | 'erro';

export interface AnaliseSessao extends ResultadoQuadro {
  em: Date;
}

export interface ContadoresSessao {
  analises: number;
  ocorrencias: number;
  alertas: number;
  falhas: number;
}

const CONTADORES_ZERADOS: ContadoresSessao = { analises: 0, ocorrencias: 0, alertas: 0, falhas: 0 };

function mensagemDeCamera(erro: unknown): string {
  const nome = erro instanceof DOMException ? erro.name : '';
  if (nome === 'NotAllowedError') return 'A câmera está bloqueada. Libere a permissão de câmera para este site e tente de novo.';
  if (nome === 'NotFoundError') return 'Nenhuma câmera foi encontrada neste dispositivo.';
  if (nome === 'NotReadableError') return 'A câmera está em uso por outro aplicativo.';
  return 'Não foi possível abrir a câmera.';
}

export function useMonitoramentoCamera(cameraId: string) {
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const timerRef = useRef<number | null>(null);
  const wakeLockRef = useRef<WakeLockSentinel | null>(null);
  const amostraAnteriorRef = useRef<Uint8Array | null>(null);
  const movimentoPendenteRef = useRef(false);
  const ultimoEnvioRef = useRef(0);
  const enviandoRef = useRef(false);
  const ativoRef = useRef(false);

  const [estado, setEstado] = useState<EstadoMonitoramento>('parado');
  const [erro, setErro] = useState<string | null>(null);
  const [movimento, setMovimento] = useState(false);
  const [ultimaAnalise, setUltimaAnalise] = useState<AnaliseSessao | null>(null);
  const [contadores, setContadores] = useState<ContadoresSessao>(CONTADORES_ZERADOS);
  const [ocorrenciasSessao, setOcorrenciasSessao] = useState<AnaliseSessao[]>([]);
  const [segundosParaProxima, setSegundosParaProxima] = useState(0);

  const parar = useCallback(() => {
    ativoRef.current = false;
    if (timerRef.current !== null) window.clearInterval(timerRef.current);
    timerRef.current = null;
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
    if (videoRef.current) videoRef.current.srcObject = null;
    void wakeLockRef.current?.release().catch(() => undefined);
    wakeLockRef.current = null;
    amostraAnteriorRef.current = null;
    movimentoPendenteRef.current = false;
    setMovimento(false);
    setEstado((atual) => (atual === 'erro' ? atual : 'parado'));
  }, []);

  const capturarFoto = useCallback(async (video: HTMLVideoElement): Promise<Blob | null> => {
    const escala = Math.min(1, LADO_MAXIMO_FOTO / Math.max(video.videoWidth, video.videoHeight));
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(video.videoWidth * escala);
    canvas.height = Math.round(video.videoHeight * escala);
    canvas.getContext('2d')?.drawImage(video, 0, 0, canvas.width, canvas.height);
    return new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', QUALIDADE_JPEG));
  }, []);

  const enviar = useCallback(async (video: HTMLVideoElement) => {
    enviandoRef.current = true;
    ultimoEnvioRef.current = Date.now();
    movimentoPendenteRef.current = false;
    try {
      const foto = await capturarFoto(video);
      if (!foto || !ativoRef.current) return;
      const resultado = await api.cameras.analisarQuadro(cameraId, foto, new Date());
      if (!ativoRef.current) return;
      if (resultado.situacao === 'ignorado') return;
      const analise: AnaliseSessao = { ...resultado, em: new Date() };
      setUltimaAnalise(analise);
      setContadores((c) => ({
        analises: c.analises + 1,
        ocorrencias: c.ocorrencias + (resultado.situacao === 'ocorrencia' ? 1 : 0),
        alertas: c.alertas + (resultado.alertaEnviado ? 1 : 0),
        falhas: c.falhas + (resultado.situacao === 'falha' ? 1 : 0),
      }));
      if (resultado.situacao === 'ocorrencia') setOcorrenciasSessao((lista) => [analise, ...lista].slice(0, 20));
      setErro(null);
    } catch (e) {
      // Erro de rede ou câmera desligada no cadastro: mostra o aviso e segue tentando no próximo ciclo.
      setErro(e instanceof Error ? e.message : 'Não foi possível enviar a foto para análise.');
    } finally {
      enviandoRef.current = false;
    }
  }, [cameraId, capturarFoto]);

  const amostrar = useCallback(() => {
    const video = videoRef.current;
    if (!video || !ativoRef.current || video.readyState < 2 || video.videoWidth === 0) return;

    const canvas = document.createElement('canvas');
    canvas.width = LARGURA_AMOSTRA;
    canvas.height = ALTURA_AMOSTRA;
    const ctx = canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;
    ctx.drawImage(video, 0, 0, LARGURA_AMOSTRA, ALTURA_AMOSTRA);
    const atual = paraCinza(ctx.getImageData(0, 0, LARGURA_AMOSTRA, ALTURA_AMOSTRA).data);

    if (houveMovimento(amostraAnteriorRef.current, atual)) movimentoPendenteRef.current = true;
    amostraAnteriorRef.current = atual;
    setMovimento(movimentoPendenteRef.current);

    const restante = INTERVALO_ENVIO_MS - (Date.now() - ultimoEnvioRef.current);
    setSegundosParaProxima(Math.max(0, Math.ceil(restante / 1000)));

    if (movimentoPendenteRef.current && restante <= 0 && !enviandoRef.current) void enviar(video);
  }, [enviar]);

  const iniciar = useCallback(async () => {
    if (ativoRef.current) return;
    setErro(null);
    setEstado('iniciando');
    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: { ideal: 'environment' }, width: { ideal: 1280 }, height: { ideal: 720 } },
        audio: false,
      });
      streamRef.current = stream;
      if (videoRef.current) {
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
      }
      // Mantém a tela do tablet acesa quando o navegador suporta; sem suporte, o aviso da tela cobre.
      try {
        wakeLockRef.current = (await navigator.wakeLock?.request('screen')) ?? null;
      } catch {
        wakeLockRef.current = null;
      }
      ativoRef.current = true;
      ultimoEnvioRef.current = 0;
      setContadores(CONTADORES_ZERADOS);
      setOcorrenciasSessao([]);
      setUltimaAnalise(null);
      timerRef.current = window.setInterval(amostrar, INTERVALO_AMOSTRA_MS);
      setEstado('monitorando');
    } catch (e) {
      setErro(mensagemDeCamera(e));
      setEstado('erro');
      parar();
    }
  }, [amostrar, parar]);

  // Sai da página ou troca de câmera: libera a câmera do dispositivo.
  useEffect(() => parar, [parar, cameraId]);

  return { estado, erro, videoRef, iniciar, parar, ultimaAnalise, contadores, ocorrenciasSessao, segundosParaProxima, movimento };
}
```

Se o `tsconfig` do projeto não tiver o tipo `WakeLockSentinel` (lib DOM antiga), declarar localmente `type TravaTela = { release: () => Promise<void> }` e usar `(navigator as Navigator & { wakeLock?: { request: (t: 'screen') => Promise<TravaTela> } }).wakeLock`.

- [ ] **Step 3: Verificar e commitar**

Run: `npm run build` e `npm run lint`
Expected: sem erros (o hook ainda não é usado; se o lint acusar export não usado como erro, seguir — a Task 3 o consome; se bloquear o commit, juntar com a Task 3).

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/cameras/detectorMovimento.ts src/AAHBRANT.SST.TeamsApp/src/pages/cameras/useMonitoramentoCamera.ts
git commit -m "feat(cameras): detector de movimento e hook de monitoramento pela câmera do dispositivo"
```

---

### Task 3: Tela Monitorar

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/cameras/rotulos.ts`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/cameras/MonitorarCameraPage.tsx`

**Interfaces:**
- Consumes: `useMonitoramentoCamera` (Task 2), `useCameras`, `tipoCameraLabel` (Task 1).
- Produces: `export function MonitorarCameraPage()` (lê `:id` da rota); de `rotulos.ts`: `descreverIrregularidade(semCapacete, semColete): string`, `resumoCena(pessoas, semCapacete, semColete): string`, `estadoDaCamera(camera, agora?): { rotulo: string; tom: Tom }`, `formatarHora(data: Date | string): string`.

- [ ] **Step 1: Rótulos compartilhados**

```ts
import type { Tom } from '@ui';
import type { CameraResumo } from '../../lib/api';

const DOIS_MINUTOS_MS = 2 * 60 * 1000;

export function descreverIrregularidade(semCapacete: number, semColete: number): string {
  if (semCapacete > 0 && semColete > 0) return 'Sem capacete e sem colete';
  if (semCapacete > 0) return 'Sem capacete';
  if (semColete > 0) return 'Sem colete';
  return 'Cena regular';
}

export function resumoCena(pessoas: number, semCapacete: number, semColete: number): string {
  const partes = [`${pessoas} ${pessoas === 1 ? 'pessoa' : 'pessoas'} na cena`];
  if (semCapacete > 0) partes.push(`${semCapacete} sem capacete`);
  if (semColete > 0) partes.push(`${semColete} sem colete`);
  return partes.join(', ');
}

export function estadoDaCamera(camera: CameraResumo, agora = Date.now()): { rotulo: string; tom: Tom } {
  if (!camera.habilitada) return { rotulo: 'Desligada', tom: 'neutro' };
  const ultima = camera.ultimaAnaliseEmUtc ? new Date(camera.ultimaAnaliseEmUtc).getTime() : 0;
  if (ultima && agora - ultima < DOIS_MINUTOS_MS) return { rotulo: 'Monitorando', tom: 'ok' };
  return { rotulo: 'Sem análise recente', tom: 'atencao' };
}

export function formatarHora(data: Date | string): string {
  return new Date(data).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

export function formatarDataHora(data: Date | string): string {
  return new Date(data).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
}
```

Conferir o formato do JSON de datas do backend: se `ultimaAnaliseEmUtc` vier sem sufixo `Z`, o `new Date()` interpreta como horário local. Ver como outras telas tratam datas UTC do backend (procurar um helper em `src/lib` com "Utc" no nome) e usar o mesmo helper aqui.

- [ ] **Step 2: Página**

Ler antes: `Monitorar.dc.html` do mockup; uma página de detalhe existente que use `BotaoVoltar` e `makeStyles` com `tokensUi`/`designTokens` (ex.: `pages/suporte-ia/SuporteIaDetalhePage.tsx`) para copiar o padrão de estilos e de cabeçalho.

Estrutura obrigatória (de cima para baixo, igual ao mockup):
1. Cabeçalho: `BotaoVoltar` para `/cameras`, título `Monitorar — {camera.nome}`, subtítulo `{obraNome} · {areaNome ?? 'Sem área definida'} · {tipoCameraLabel[tipo]}`, e à direita um `StatusChip` com o estado: `Monitorando` (tom `ok`), `Abrindo a câmera…` (`info`), `Parado` (`neutro`), `Câmera indisponível` (`alerta`).
2. Duas colunas que empilham em tela estreita (`flex-wrap: wrap`; vídeo `flex: 999 1 560px`, painel `flex: 1 1 320px`).
3. Coluna do vídeo: `<video ref={videoRef} playsInline muted>` em caixa 16:9 com `object-fit: cover`; sobre ele, no canto superior esquerdo, `Movimento detectado · analisando` quando `movimento` é verdadeiro, senão `Cena parada`; no canto inferior direito, `Próxima análise liberada em {segundosParaProxima} s` quando > 0. Abaixo, um aviso (`FeedbackInline` de atenção) com o texto: "Mantenha esta tela aberta e o tablet ligado na tomada. Se a tela bloquear ou a página fechar, o monitoramento para. A foto só é enviada para análise quando há movimento, no máximo uma a cada 30 segundos."
4. Painel: card "Última análise · {hora}" com `descreverIrregularidade` em `StatusChip` (tom `alerta` se ocorrência, `ok` se limpo, `atencao` se falha → texto "Análise não concluída") e `resumoCena`; linha "Alerta enviado ao Telegram" ou "Alerta não enviado"; três `KpiCard` (ou cards simples) — Análises, Ocorrências, Alertas enviados — com os `contadores`; card "Ocorrências desta sessão" listando `ocorrenciasSessao` (hora · irregularidade · resumo) com link "Ver todas" para `/cameras?aba=alertas`; botão principal: `Iniciar monitoramento` quando parado/erro, `Parar monitoramento` quando monitorando.
5. `erro` do hook aparece em `FeedbackInline` de alerta acima das colunas.
6. Câmera não encontrada na lista (id inválido ou fora do escopo): `EstadoVazio` com "Câmera não encontrada" e botão para voltar. Câmera com `habilitada === false`: mostrar aviso "O monitoramento desta câmera está desligado no cadastro." e manter o botão Iniciar desabilitado.

Esqueleto do componente (estilos com `makeStyles` e tokens, seguindo a página de referência):

```tsx
export function MonitorarCameraPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const { data: cameras, isLoading } = useCameras();
  const camera = cameras?.find((c) => c.id === id);
  const m = useMonitoramentoCamera(id);

  if (isLoading) return <Carregando variante="detalhe" linhas={6} />;
  if (!camera) {
    return <EstadoVazio titulo="Câmera não encontrada" descricao="Ela pode ter sido excluída ou pertencer a outra obra." />;
  }
  const monitorando = m.estado === 'monitorando' || m.estado === 'iniciando';
  // ... JSX conforme a estrutura acima
}
```

Conferir as props reais de `EstadoVazio`, `StatusChip`, `FeedbackInline`, `KpiCard` e `BotaoVoltar` nos arquivos em `src/ui/` antes de usar; os nomes de props acima são ilustrativos.

- [ ] **Step 3: Verificar e commitar**

Run: `npm run build` e `npm run lint`
Expected: sem erros.

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/cameras
git commit -m "feat(cameras): tela Monitorar com vídeo do dispositivo e resultado das análises"
```

---

### Task 4: Página Câmeras com mosaico e cadastro

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/cameras/CamerasPage.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/cameras/MosaicoCamerasTab.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/cameras/FormularioCamera.tsx`

**Interfaces:**
- Consumes: `useCameras`, `useObras`, `api.cameras.criar/atualizar/excluir`, `estadoDaCamera`, `tipoCameraLabel`, `TipoCamera`, `useSouAdministrador` (`src/lib/UsuarioLogadoContext`).
- Produces: `export function CamerasPage()` — abas com chaves `cameras` e `alertas` controladas por `useAbaNaUrl` (parâmetro `aba`); a aba `alertas` renderiza `<AlertasEpiTab />` (Task 5 — nesta tarefa, um `EstadoVazio` provisório com o texto "Alertas de EPI" que a Task 5 substitui).

- [ ] **Step 1: Ler os padrões**

`Main.dc.html` do mockup; `pages/inspecoes/VeiculosTab.tsx` (cadastro inline com `PainelCriacaoInline`, `FormSection`, `FormGrid`, `Campo`, `FormRodape`, `useConfirmar`, `useSucessoToast`, exclusão só para administrador); uma página com `PageHeader` + `Abas` + `useAbaNaUrl` (ex.: `pages/operacao/`); como as telas listam áreas de uma obra (`api` de `areasSst`, procurar `areas` em `lib/api.ts`).

- [ ] **Step 2: `CamerasPage`**

`PageHeader` com título "Câmeras" e descrição "Monitoramento de capacete e colete por imagem, com alerta no Telegram."; `Abas` com `{ chave: 'cameras', rotulo: 'Câmeras' }` e `{ chave: 'alertas', rotulo: 'Alertas de EPI' }`.

- [ ] **Step 3: `MosaicoCamerasTab`**

Comportamento:
- Barra de ações: alternância **mosaico / lista** (dois botões com `aria-pressed`, ícones `Grid24Regular` e `List24Regular`, preferência guardada em `sessionStorage` na chave `sst:camerasVisao`), botão **Tela cheia** (`document.fullscreenElement ? document.exitFullscreen() : contêiner.requestFullscreen()`, com `try/catch` silencioso; esconder o botão se `!document.fullscreenEnabled`) e botão **Nova câmera** (abre `FormularioCamera` em `PainelCriacaoInline`).
- Filtro por obra em botões-cartão: "Todas as obras ({n} câmeras)" e um por obra que tem câmera, com a contagem; o selecionado tem `aria-pressed="true"` e borda de destaque.
- Agrupamento: um bloco por obra com cabeçalho "{obra} ({n} câmeras)" (ícone de pasta) e a grade de blocos (`grid-template-columns: repeat(auto-fill, minmax(280px, 1fr))`).
- Bloco de câmera (mosaico): área 16:9 clicável (`<Link to={`/cameras/${id}/monitorar`}>` com `aria-label="Abrir monitoramento da câmera {nome}"`) mostrando ícone de câmera e o texto do estado (`Monitoramento desligado` quando desligada; sem vídeo nesta fase); selo com `{ocorrenciasHoje} ocorrências em 24 h` quando > 0; rodapé com nome, `{tipo} · última análise {hora}` ou "ainda sem análise", indicador de estado (`estadoDaCamera`: forma + texto, não só cor) e um menu/botões Editar e, para administrador, Excluir (com `useConfirmar`: "Excluir a câmera {nome}? As ocorrências já registradas continuam guardadas.").
- Visão em lista: `DataTable` com colunas Câmera (nome + obra), Área, Tipo, Monitoramento (estado), Intervalo entre alertas (`0` → "Sem limite", senão "{n} minutos"), Última análise, Ocorrências 24 h, Análises (`totalAnalises`), ações (Monitorar, Editar, Excluir).
- Sem câmeras: `EstadoVazio` "Nenhuma câmera cadastrada" com ação "Nova câmera".
- Rodapé informativo (`FeedbackInline` de informação): "Nesta fase só o dispositivo que está monitorando mostra a imagem ao vivo, da própria câmera. O mosaico com várias imagens ao vivo depende das câmeras fixas da obra."
- Após criar/editar/excluir: invalidar a consulta de câmeras (`queryClient.invalidateQueries({ queryKey: ['cameras'] })`) e mostrar toast de sucesso.

- [ ] **Step 4: `FormularioCamera`**

Campos: Obra (`Select`, obrigatório; desabilitado na edição, porque a API não muda a obra), Nome (`Input`, obrigatório, máx. 100), Área (`Select` opcional com as áreas da obra escolhida; primeira opção "Sem área definida"), Tipo (`Select`: Canteiro, Portaria), Monitoramento ligado (`Switch`), Intervalo entre alertas (`Select`: "Sem limite — alerta a cada ocorrência" = 0, 5, 10, 30, 60 minutos). Valores iniciais de criação: obra do filtro atual (ou `escolherObraInicial` de `hooks/useObraLembrada`), tipo Canteiro, ligado, intervalo 0. Erros da API (nome repetido, área de outra obra) aparecem em `FeedbackInline` dentro do formulário, com a mensagem devolvida.

Props: `{ camera?: CameraResumo; obras: Obra[]; obraInicial?: string; aoSalvar: () => void; aoCancelar: () => void }`.

- [ ] **Step 5: Verificar e commitar**

Run: `npm run build` e `npm run lint`
Expected: sem erros.

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/cameras
git commit -m "feat(cameras): página Câmeras com mosaico, lista e cadastro"
```

---

### Task 5: Aba Alertas de EPI

**Files:**
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/cameras/FotoOcorrenciaEpi.tsx`
- Create: `src/AAHBRANT.SST.TeamsApp/src/pages/cameras/AlertasEpiTab.tsx`
- Modify: `src/AAHBRANT.SST.TeamsApp/src/pages/cameras/CamerasPage.tsx` (trocar o provisório pela aba real)

**Interfaces:**
- Consumes: `useOcorrenciasEpi`, `useCameras`, `useObras`, `api.cameras.baixarFotoOcorrencia/vincularTrabalhador/definirSituacao`, `SituacaoOcorrenciaEpi`, `situacaoOcorrenciaEpiLabel`, `descreverIrregularidade`, `resumoCena`, `formatarDataHora`.
- Produces: `export function AlertasEpiTab()`, `export function FotoOcorrenciaEpi({ id, alt }: { id: string; alt: string })`.

- [ ] **Step 1: `FotoOcorrenciaEpi`**

Mesmo mecanismo de `components/camera/SlotFotoRemota.tsx` (baixar `Blob`, `URL.createObjectURL`, revogar no cleanup, flag `cancelado`), mas só exibição: caixa 16:9, `Carregando` enquanto baixa, `<img alt={alt}>` com `object-fit: cover` quando pronto, texto "Foto indisponível" em caso de erro. Carregar só quando o cartão entra na tela (`IntersectionObserver` com `rootMargin: '200px'`), para não baixar 200 fotos de uma vez.

- [ ] **Step 2: `AlertasEpiTab`**

Ler `Ocorrencias.dc.html` do mockup e como outra tela usa `SeletorPesquisavel` com trabalhadores (procurar `SeletorPesquisavel` em `src/pages` — reusar a mesma fonte de dados e o mesmo formato de `OpcaoSeletor`).

Comportamento:
- Filtros: Obra (padrão: obra lembrada/todas), Câmera (as da obra escolhida; "Todas"), Período (`Hoje`, `Últimos 7 dias`, `Últimos 30 dias` — converte para `de` em ISO; `ate` vazio), Situação (`Todas`, `Novas`, `Conferidas`, `Falso alerta`). Ao lado, o resumo "{n} ocorrências · {novas} novas". Se a lista vier com 200 itens, mostrar "Mostrando as 200 mais recentes; refine o período."
- Grade de cartões (`repeat(auto-fill, minmax(340px, 1fr))`), cada um: `FotoOcorrenciaEpi`; sobre a foto, `StatusChip` da irregularidade (tom `alerta`) e da situação (`Nova` = `info`, `Conferida` = `ok`, `Falso alerta` = `neutro`); título "{cameraNome} · {data hora}"; `resumoCena`; linha "Alerta enviado ao Telegram" ou "Alerta não enviado"; se há trabalhador vinculado, uma faixa com o nome; ações:
  - sem trabalhador: **Vincular trabalhador** (abre `SeletorPesquisavel` em diálogo/painel; ao escolher, `api.cameras.vincularTrabalhador(id, trabalhadorId)`);
  - com trabalhador: **Trocar trabalhador** e **Remover vínculo** (`vincularTrabalhador(id, null)`);
  - situação ≠ Falso alerta: **Falso alerta** (`definirSituacao(id, 3)`, com `useConfirmar`: "Marcar como falso alerta? Use quando a IA errou — a pessoa estava com o EPI.");
  - situação = Falso alerta: **Desfazer falso alerta** (`definirSituacao(id, trabalhadorId ? 2 : 1)`).
- Após cada ação: invalidar `['cameras', 'ocorrencias']` e toast de sucesso; erro em `FeedbackInline`.
- Lista vazia: `EstadoVazio` "Nenhuma ocorrência no período" com a descrição "Quando uma câmera registrar alguém sem capacete ou colete, o alerta aparece aqui."
- Rodapé pequeno: "As fotos mostram trabalhadores. O acesso segue o escopo por obra."

- [ ] **Step 3: Ligar na página, verificar e commitar**

Run: `npm run build` e `npm run lint`
Expected: sem erros.

```bash
git add src/AAHBRANT.SST.TeamsApp/src/pages/cameras
git commit -m "feat(cameras): aba Alertas de EPI com foto, vínculo de trabalhador e falso alerta"
```

---

### Task 6: Menu e rotas com guarda de acesso

**Files:**
- Modify: `src/AAHBRANT.SST.TeamsApp/src/layout/AppShell.tsx` (lista `itensPilares`, ≈ linha 690, e onde ela é renderizada, ≈ linha 1047)
- Modify: `src/AAHBRANT.SST.TeamsApp/src/App.tsx`

**Interfaces:**
- Consumes: `useAcessoCameras` (Task 1), `CamerasPage` (Task 4), `MonitorarCameraPage` (Task 3).

- [ ] **Step 1: Item condicional na barra lateral**

`itensPilares` é uma constante de módulo e a ordem importa: "Câmeras" entra logo depois de "Operação". Não inserir na constante (ela não conhece o usuário): no componente que faz `itensPilares.map(...)`, montar a lista efetiva com `useMemo`:

```tsx
const acessoCameras = useAcessoCameras();
const itensPilaresVisiveis = useMemo(() => {
  if (!acessoCameras) return itensPilares;
  const posicao = itensPilares.findIndex((i) => i.rota === '/operacao') + 1;
  return [
    ...itensPilares.slice(0, posicao),
    { rota: '/cameras', rotulo: 'Câmeras', icone: Video24Regular },
    ...itensPilares.slice(posicao),
  ];
}, [acessoCameras]);
```

e trocar `itensPilares.map` por `itensPilaresVisiveis.map`. Importar `Video24Regular` de `@fluentui/react-icons`. Verificar se há outra lista derivada de `itensPilares` (menu compacto de telas estreitas, busca de navegação, trilha/breadcrumb): procurar por `itensPilares` e por `'/operacao'` em `src/layout` e `src/components`; onde a lista de módulos for usada para navegação, aplicar a mesma lista efetiva. Onde for só um mapa de rótulos por rota (trilha), acrescentar `/cameras` → "Câmeras".

- [ ] **Step 2: Rotas com guarda**

Em `App.tsx`, junto das rotas internas (dentro do mesmo layout/`Suspense` das demais páginas), com import `lazy` se esse for o padrão do arquivo:

```tsx
function SomenteComAcessoCameras({ children }: { children: ReactElement }) {
  const { data, isLoading } = useQuery({
    queryKey: chaves.camerasAcesso,
    queryFn: () => api.cameras.acesso().catch(() => ({ permitido: false })),
    staleTime: 5 * 60 * 1000,
  });
  if (isLoading) return <Carregando variante="detalhe" linhas={6} />;
  return data?.permitido ? children : <Navigate to="/" replace />;
}

<Route path="/cameras" element={<SomenteComAcessoCameras><CamerasPage /></SomenteComAcessoCameras>} />
<Route path="/cameras/:id/monitorar" element={<SomenteComAcessoCameras><MonitorarCameraPage /></SomenteComAcessoCameras>} />
```

A guarda precisa distinguir "carregando" de "sem acesso" (por isso não usa o `useAcessoCameras` booleano). Se preferir, exportar de `consultas.ts` um segundo hook `useAcessoCamerasEstado()` que devolve `{ permitido, carregando }` e usar aqui — sem duplicar a `queryFn`.

- [ ] **Step 3: Verificar e commitar**

Run: `npm run build` e `npm run lint`
Expected: sem erros.

```bash
git add src/AAHBRANT.SST.TeamsApp/src/layout/AppShell.tsx src/AAHBRANT.SST.TeamsApp/src/App.tsx src/AAHBRANT.SST.TeamsApp/src/lib/consultas.ts
git commit -m "feat(cameras): item Câmeras na barra lateral e rotas restritas ao piloto"
```

---

### Task 7: Verificação no navegador (com o usuário)

Esta tarefa depende de autorização do usuário: aplicar a migration `CriarModuloCameras` no banco **local** (aditiva: cria duas tabelas) e usar as chaves do Claude e do Telegram. Não executar sem o "ok" explícito.

- [ ] **Step 1: Subir o ambiente local**

Docker aberto com o SQL Server local. Portas próprias para esta worktree (há outras worktrees rodando; colisão de porta e `.env.local` não relido pelo Vite são problemas conhecidos): conferir `.claude/launch.json` e o `.env.local` do app antes de subir.

- [ ] **Step 2: Roteiro**

1. Menu mostra **Câmeras** abaixo de Operação (em dev local o acesso é aberto; a restrição só vale com Entra ligado).
2. Criar uma câmera "Tablet teste" numa obra; ela aparece no mosaico como "Sem análise recente"; alternar para lista e voltar; Tela cheia entra e sai.
3. Abrir Monitorar → Iniciar: o navegador pede a câmera; o vídeo aparece; "Cena parada" com a cena imóvel; ao mexer na frente da câmera, "Movimento detectado · analisando" e, em até 30 s, a "Última análise" preenche.
4. Aba de rede do navegador: `POST /api/cameras/{id}/quadros` só sai com movimento e nunca com menos de 30 s de intervalo; com a cena parada por 2 min, nenhuma chamada.
5. Com pessoa sem capacete: resultado "Sem capacete", contador de ocorrências sobe e a mensagem chega no grupo do Telegram (se `Telegram:BotToken` estiver configurado; senão aparece "Alerta não enviado").
6. Aba Alertas de EPI: o cartão aparece com a foto; vincular trabalhador, marcar e desfazer falso alerta.
7. Parar monitoramento: a luz da câmera apaga; sair da página também libera a câmera.
8. Largura de celular (375 px): colunas empilham, botões continuam alcançáveis, sem rolagem horizontal.

- [ ] **Step 3: Registrar**

Anotar no relatório: quantas análises o roteiro consumiu (`totalAnalises` da câmera), acertos e erros da IA observados, e qualquer ajuste necessário nos limiares do detector (`LIMIAR_PIXEL`, `LIMIAR_FRACAO`).

---

## Cobertura da spec

| Spec | Onde |
| --- | --- |
| §2 passos 1–3 (tela, movimento, teto de envio) | Tasks 2 e 3 |
| §3 fonte de imagem e detector de movimento | Task 2 |
| §5 tela Câmeras (cadastro) | Task 4 |
| §5 tela Monitorar | Task 3 |
| §5 tela Ocorrências (vincular, falso alerta) | Task 5 |
| §7 escopo e acesso restrito ao piloto | Task 6 (menu e rotas) + backend |
| §7 "recurso indisponível" | Task 3: resultado `falha` aparece como "Análise não concluída"; erro de envio em aviso |
| §8 tela aberta no tablet | Task 2 (wake lock) e aviso fixo na Task 3 |
| §10 detector com quadros sintéticos | **Não coberto por teste automático** — o frontend não tem runner de teste unitário e o plano não adiciona dependência; a função é pura e fica pronta para teste quando houver runner. Verificação pelo roteiro da Task 7 (passo 4). |
| §10 piloto real | Task 7 |

## Decisões para o usuário (não bloqueiam a execução)

1. **Runner de teste no frontend:** adicionar `vitest` (dependência de desenvolvimento) permitiria testar automaticamente o detector de movimento, que é o que protege o custo. O plano segue sem ele.
2. **Novidade no pop-up:** fica para quando o piloto for aberto a mais usuários.
