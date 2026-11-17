import { leituraParaLocalizacao, type ContextoFoto, type DadosFoto, type LocalizacaoFoto } from './dadosFoto';
import { desenharCarimbo, enderecoDaPosicao, linhasCarimbo, type ObservadorEndereco } from './carimboFoto';
import type { Rastreador } from './rastreadorLocalizacao';

// Quanto o clique em "capturar" espera pela posição quando ela ainda não está boa. O quadro já foi
// congelado no clique; a posição que chegar nesse intervalo fica dentro da janela de 60 s.
export const ESPERA_LOCALIZACAO_NA_CAPTURA_MS = 15_000;

export interface OpcoesEvidencia {
  /** Quadro já congelado (a hora da captura é a do clique, não a de agora). */
  canvas: HTMLCanvasElement;
  contextoFoto: ContextoFoto;
  /** Texto do local que entra no carimbo ao lado da obra (ex.: "NR-06 · Foto exclusiva desta etapa"). */
  localFoto: string;
  capturadaEm: Date;
  rastreador: Rastreador;
  endereco: ObservadorEndereco | null;
  /** Desenha o carimbo na imagem (foto de evidência com câmera traseira). A facial nunca é carimbada. */
  carimbar: boolean;
  /** Avisa a tela de que está esperando a posição ou o endereço (para o spinner). */
  aoOcupar?: (motivo: 'localizacao' | 'endereco', ativo: boolean) => void;
}

// Pipeline da foto de evidência (extraído de useCapturaFoto em 16/11/2026 sem mudança de comportamento, para a
// foto manual e as fotos automáticas da integração passarem exatamente pelo mesmo caminho): espera a posição
// pronta, carimba data/hora/posição/endereço/local na imagem e monta os metadados (DadosFoto) que o envio exige.
// O envio em si (token, multipart, hash no servidor) é de api.sessoesTreinamento.anexarFotoEvidencia.
export async function prepararDadosEvidencia(o: OpcoesEvidencia): Promise<DadosFoto> {
  const { canvas, contextoFoto, localFoto, capturadaEm, rastreador: r, endereco, carimbar, aoOcupar } = o;
  // O rastreio roda desde a abertura, então a posição normalmente já está pronta. Se ainda não
  // estiver (GPS frio, precisão ruim), espera um pouco — o quadro já foi congelado, então a hora
  // da captura continua sendo a do clique.
  let e = r.estado();
  if (e.status === 'buscando' || e.status === 'imprecisa') {
    aoOcupar?.('localizacao', true);
    try {
      await r.aguardarPronta(ESPERA_LOCALIZACAO_NA_CAPTURA_MS);
    } finally {
      aoOcupar?.('localizacao', false);
    }
    e = r.estado();
  }
  const loc: LocalizacaoFoto = e.leitura
    ? leituraParaLocalizacao(e.leitura)
    : { motivoLocalizacao: e.motivo ?? 'A localização não chegou a tempo. Tente novamente no local.' };
  if (carimbar) {
    const ctx = canvas.getContext('2d');
    const l = e.leitura;
    let linhasEndereco: string[] = [];
    if (l) {
      linhasEndereco = endereco?.linhas(l.latitude, l.longitude) ?? [];
      if (linhasEndereco.length === 0) {
        aoOcupar?.('endereco', true);
        try {
          linhasEndereco = await enderecoDaPosicao(l.latitude, l.longitude);
        } finally {
          aoOcupar?.('endereco', false);
        }
      }
    }
    const nota = [contextoFoto.obraNome, localFoto.trim()].filter(Boolean).join(' · ');
    if (ctx) {
      desenharCarimbo(
        ctx,
        canvas.width,
        canvas.height,
        linhasCarimbo(
          capturadaEm.toISOString(),
          { latitude: l?.latitude, longitude: l?.longitude, altitudeMetros: l?.altitudeMetros, precisaoMetros: l?.precisaoMetros, motivo: loc.motivoLocalizacao },
          nota,
          linhasEndereco,
        ),
      );
    }
  }
  return {
    ...loc,
    origem: 'camera',
    registroId: contextoFoto.registroId,
    capturadaEm: capturadaEm.toISOString(),
    fusoMinutos: capturadaEm.getTimezoneOffset(),
    obraId: contextoFoto.obraId,
    obraNome: contextoFoto.obraNome,
    local: localFoto.trim(),
  };
}
