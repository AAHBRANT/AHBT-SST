import { useEffect, useRef, useState } from 'react';
import { FeedbackInline } from '@ui';
import { api, type SessaoTreinamentoDetalhe } from '../../lib/api';
import { GradeFotosEvidencia } from '../GradeFotosEvidencia';

export const TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS = 3;

interface Props {
  detalhe: SessaoTreinamentoDetalhe;
  somenteLeitura: boolean;
  aoAtualizar: () => Promise<void> | void;
  /**
   * Recebe a mensagem de erro (inclusive a recusa do servidor). Sem isso, o erro aparece aqui mesmo,
   * acima das fotos.
   */
  aoErro?: (mensagem: string | null) => void;
  titulo?: string;
  subtitulo?: string;
  /** Só câmera ao vivo, sem galeria nem arquivo (fotos de evidência da integração). */
  exigirCamera?: boolean;
  /** Texto que entra no carimbo da foto, ao lado da obra (ex.: "NR-06 · Foto exclusiva desta etapa"). */
  localFoto?: string;
  /** Pede um token de captura ao abrir a câmera e o envia junto com a foto (evidência da integração). */
  exigirTokenCaptura?: boolean;
}

// Evidências fotográficas da turma, extraídas de SessaoTreinamentoDetalhePage em 11/11/2026 sem mudança
// visual. Na integração em etapas cada etapa é uma turma com as próprias 3 fotos, tiradas ao vivo, e o
// servidor recusa foto já usada em outra etapa ("Esta foto já foi usada na NR-xx. Tire uma nova foto
// desta etapa."): a mensagem do servidor é repassada sem alteração.
export function FotosTurma({ detalhe, somenteLeitura, aoAtualizar, aoErro, titulo = 'Evidências fotográficas', subtitulo, exigirCamera, localFoto, exigirTokenCaptura }: Props) {
  const sessao = detalhe.sessao;
  const [previews, setPreviews] = useState<Record<string, string>>({});
  const [erroLocal, setErroLocal] = useState<string | null>(null);

  const chaveFotos = detalhe.fotosEvidencia.map((f) => f.id).join('|');
  useEffect(() => {
    let cancelado = false;
    const criadas: string[] = [];
    void Promise.all(
      detalhe.fotosEvidencia.map(async (foto) => {
        try {
          const blob = await api.sessoesTreinamento.baixarFotoEvidencia(foto.id);
          const url = URL.createObjectURL(blob);
          criadas.push(url);
          return [foto.id, url] as const;
        } catch {
          return null;
        }
      }),
    ).then((lista) => {
      if (cancelado) return;
      setPreviews(Object.fromEntries(lista.filter((p): p is [string, string] => p !== null)));
    });
    return () => {
      cancelado = true;
      criadas.forEach((u) => URL.revokeObjectURL(u));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [chaveFotos]);

  // Token de captura por quadro: pedido ao abrir a câmera (uso único, 5 min). Se expirar, pede outro
  // e reenvia a mesma foto, sem o usuário tirar de novo.
  const tokens = useRef(new Map<number, { token: string; expiraEmUtc: string }>());
  async function pedirToken(ordem: number) {
    try {
      tokens.current.set(ordem, await api.sessoesTreinamento.obterTokenCapturaFoto(sessao.id));
    } catch {
      tokens.current.delete(ordem);
    }
  }
  async function tokenValido(ordem: number): Promise<string | null> {
    const atual = tokens.current.get(ordem);
    if (!atual || Date.parse(atual.expiraEmUtc) <= Date.now() + 5_000) await pedirToken(ordem);
    return tokens.current.get(ordem)?.token ?? null;
  }

  function reportar(mensagem: string | null) {
    if (aoErro) aoErro(mensagem);
    else setErroLocal(mensagem);
  }

  async function anexar(ordem: number, arquivo: File) {
    try {
      reportar(null);
      if (!exigirTokenCaptura) {
        await api.sessoesTreinamento.anexarFotoEvidencia(sessao.id, ordem, arquivo);
      } else {
        try {
          await api.sessoesTreinamento.anexarFotoEvidencia(sessao.id, ordem, arquivo, await tokenValido(ordem));
        } catch (e) {
          // Token vencido ou já usado: pede outro e reenvia a mesma foto (uma vez).
          if (!(e instanceof Error) || !/token/i.test(e.message)) throw e;
          tokens.current.delete(ordem);
          await api.sessoesTreinamento.anexarFotoEvidencia(sessao.id, ordem, arquivo, await tokenValido(ordem));
        } finally {
          tokens.current.delete(ordem);
        }
      }
      await aoAtualizar();
    } catch (e) {
      reportar(e instanceof Error ? e.message : 'Falha ao anexar foto de evidência.');
    }
  }

  async function remover(fotoId: string) {
    try {
      reportar(null);
      await api.sessoesTreinamento.removerFotoEvidencia(fotoId);
      await aoAtualizar();
    } catch (e) {
      reportar(e instanceof Error ? e.message : 'Falha ao remover foto de evidência.');
    }
  }

  return (
    <>
      {!aoErro && erroLocal && (
        <FeedbackInline tom="erro" aoFechar={() => setErroLocal(null)}>
          <strong>Foto recusada.</strong> {erroLocal}
        </FeedbackInline>
      )}
      <GradeFotosEvidencia
        contextoFoto={{ registroId: sessao.id, obraId: sessao.obraId, obraNome: sessao.obraNome, ...(localFoto ? { local: localFoto } : {}) }}
        titulo={titulo}
        subtitulo={subtitulo ?? `${TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS} fotos da turma são obrigatórias para liberar o encerramento.`}
        total={TOTAL_FOTOS_EVIDENCIA_OBRIGATORIAS}
        fotos={detalhe.fotosEvidencia.map((f) => ({ ordem: f.ordem, id: f.id, dadosFoto: f.dadosFoto, url: previews[f.id] }))}
        somenteLeitura={somenteLeitura}
        exigirCamera={exigirCamera}
        aoAbrirCamera={exigirTokenCaptura ? (ordem) => void pedirToken(ordem) : undefined}
        onSelecionarFoto={anexar}
        onRemoverFoto={remover}
        onErroValidacao={reportar}
      />
    </>
  );
}
