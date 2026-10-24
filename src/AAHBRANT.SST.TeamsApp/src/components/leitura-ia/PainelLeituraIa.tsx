import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { BarraProgresso, Button, FeedbackInline, Text } from '@ui';
import { Sparkle24Regular } from '@fluentui/react-icons';
import { api, StatusLeituraIa, type DocumentoLeituraIa, type LeituraIa } from '../../lib/api';

const INTERVALO_MS = 4000;

// Estado da última leitura com IA do documento, atualizado sozinho enquanto ela está na fila ou lendo.
export function useLeituraIa(documento: DocumentoLeituraIa, documentoId: string) {
  const [leitura, setLeitura] = useState<LeituraIa | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [iniciando, setIniciando] = useState(false);
  const temporizador = useRef<number | undefined>(undefined);

  const recarregar = useCallback(async () => {
    try {
      const atual = await api.leiturasIa.ultima(documento, documentoId);
      setLeitura(atual ?? null);
      if (atual && (atual.status === StatusLeituraIa.Pendente || atual.status === StatusLeituraIa.Lendo)) {
        window.clearTimeout(temporizador.current);
        temporizador.current = window.setTimeout(recarregar, INTERVALO_MS);
      }
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao consultar a leitura com IA.');
    }
  }, [documento, documentoId]);

  useEffect(() => {
    recarregar();
    return () => window.clearTimeout(temporizador.current);
  }, [recarregar]);

  async function iniciar() {
    try {
      setIniciando(true);
      setErro(null);
      await api.leiturasIa.iniciar(documento, documentoId);
      await recarregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao iniciar a leitura com IA.');
    } finally {
      setIniciando(false);
    }
  }

  const lendo = !!leitura && (leitura.status === StatusLeituraIa.Pendente || leitura.status === StatusLeituraIa.Lendo);
  return { leitura, erro, setErro, iniciar, iniciando, lendo, recarregar };
}

export function BotaoLerComIa({ aoClicar, desabilitado }: { aoClicar: () => void; desabilitado?: boolean }) {
  return (
    <Button appearance="primary" icon={<Sparkle24Regular />} onClick={aoClicar} disabled={desabilitado}>
      Ler com IA
    </Button>
  );
}

// Painel acima do PDF: progresso da leitura, leitura pronta para revisar ou falha.
export function PainelLeituraIa({
  documento,
  estado,
}: {
  documento: DocumentoLeituraIa;
  estado: ReturnType<typeof useLeituraIa>;
}) {
  const navigate = useNavigate();
  const { leitura, erro, setErro, iniciar } = estado;
  const nome = documento === 'pgr' ? 'PGR' : 'PCMSO';

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, marginBottom: 12 }}>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      {leitura && (leitura.status === StatusLeituraIa.Pendente || leitura.status === StatusLeituraIa.Lendo) && (
        <div style={{ border: '1px solid var(--sst-border-soft)', borderRadius: 10, padding: 12, display: 'grid', gap: 6 }}>
          <Text weight="semibold">Lendo o {nome} com IA…</Text>
          <BarraProgresso
            percentual={leitura.passosTotal > 0 ? Math.round((100 * leitura.passosConcluidos) / leitura.passosTotal) : 0}
            aria-label={`Progresso da leitura do ${nome}`}
          />
          <Text size={200}>
            {leitura.etapa ?? 'Na fila'} · pode sair desta tela, o aviso chega no sininho quando terminar.
          </Text>
        </div>
      )}
      {leitura?.status === StatusLeituraIa.Concluida && (
        <FeedbackInline
          tom="info"
          acao={{ rotulo: 'Revisar leitura', aoClicar: () => navigate(`/leituras-ia/${documento}/${leitura.id}`) }}
        >
          A leitura com IA do {nome} terminou e está pronta para revisão. Nada foi cadastrado ainda.
        </FeedbackInline>
      )}
      {leitura?.status === StatusLeituraIa.Falhou && (
        <FeedbackInline tom="aviso" acao={{ rotulo: 'Ler de novo', aoClicar: iniciar }}>
          A leitura com IA não terminou: {leitura.erro ?? 'erro desconhecido.'}
        </FeedbackInline>
      )}
    </div>
  );
}
