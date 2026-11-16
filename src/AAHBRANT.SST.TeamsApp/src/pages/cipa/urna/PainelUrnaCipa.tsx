import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, FeedbackInline, FormRodape, StatusChip, useConfirmar } from '@ui';
import { Open24Regular, Play24Regular, Stop24Regular } from '@fluentui/react-icons';
import { api, StatusProcessoEleitoralCipa, type UrnaCipa } from '../../../lib/api';

// Painel da urna biométrica dentro do processo eleitoral (16/11/2026): abrir a votação, abrir a tela
// do tablet, acompanhar o comparecimento e encerrar. Nunca mostra votos por candidato com a votação
// aberta — só "X de Y votaram" e o quórum de 50% da NR-5.
const STATUS_ANTES_DA_VOTACAO: number[] = [
  StatusProcessoEleitoralCipa.Convocado,
  StatusProcessoEleitoralCipa.InscricoesAbertas,
  StatusProcessoEleitoralCipa.InscricoesEncerradas,
];

export function PainelUrnaCipa({ processoId, status, aoMudar }: { processoId: string; status: number; aoMudar: () => void }) {
  const navegar = useNavigate();
  const { confirmar, dialogElement } = useConfirmar();
  const [urna, setUrna] = useState<UrnaCipa | null>(null);
  const [mensagem, setMensagem] = useState<{ tom: 'erro' | 'info' | 'sucesso' | 'aviso'; texto: string } | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const aberta = status === StatusProcessoEleitoralCipa.VotacaoAberta;

  const carregar = useCallback(() => {
    api.cipa.urna.obter(processoId).then(setUrna).catch(() => setUrna(null));
  }, [processoId]);

  useEffect(() => {
    carregar();
    if (!aberta) return;
    const t = window.setInterval(carregar, 10_000);
    return () => window.clearInterval(t);
  }, [carregar, aberta]);

  async function abrir() {
    if (!(await confirmar('Abrir a votação? A partir de agora os funcionários votam no tablet e não é mais possível inscrever candidatos.'))) return;
    try {
      setOcupado(true);
      const r = await api.cipa.urna.abrir(processoId);
      setMensagem({ tom: 'sucesso', texto: `Votação aberta para ${r.totalEleitoresAptos} eleitores.` });
      aoMudar();
    } catch (e) {
      setMensagem({ tom: 'erro', texto: e instanceof Error ? e.message : 'Falha ao abrir a votação.' });
    } finally {
      setOcupado(false);
    }
  }

  async function encerrar() {
    if (!(await confirmar('Encerrar a votação? Depois de encerrada, ninguém mais vota e os votos são contados.'))) return;
    try {
      setOcupado(true);
      const r = await api.cipa.urna.encerrar(processoId);
      setMensagem({ tom: r.apurado ? 'sucesso' : 'aviso', texto: r.mensagem });
      aoMudar();
      carregar();
    } catch (e) {
      setMensagem({ tom: 'erro', texto: e instanceof Error ? e.message : 'Falha ao encerrar a votação.' });
    } finally {
      setOcupado(false);
    }
  }

  const votantes = urna?.totalVotantes ?? 0;
  const aptos = urna?.totalEleitoresAptos ?? 0;
  const pct = aptos > 0 ? Math.round((votantes * 100) / aptos) : 0;
  const quorum = aptos > 0 && votantes * 2 >= aptos;
  const podeAbrir = STATUS_ANTES_DA_VOTACAO.includes(status);

  if (!podeAbrir && !aberta && !urna?.votacaoAbertaEmUtc) return null;

  return (
    <Card titulo="Urna biométrica" subtitulo="Votação no tablet do canteiro, por digital ou rosto. Só aparecem os candidatos deferidos.">
      {dialogElement}
      {mensagem && (
        <FeedbackInline tom={mensagem.tom} aoFechar={() => setMensagem(null)}>
          {mensagem.texto}
        </FeedbackInline>
      )}
      {urna && (aberta || urna.votacaoAbertaEmUtc) && (
        <div style={{ display: 'flex', gap: 16, alignItems: 'center', flexWrap: 'wrap' }}>
          <strong style={{ fontSize: 28, fontVariantNumeric: 'tabular-nums' }}>
            {votantes} de {aptos}
          </strong>
          <span>votaram ({pct}%)</span>
          <StatusChip tom={quorum ? 'ok' : 'atencao'}>{quorum ? 'Quórum de 50% atingido' : 'Abaixo de 50%'}</StatusChip>
          {aberta && <span style={{ opacity: 0.7, fontSize: 13 }}>Votos por candidato ficam ocultos até o encerramento.</span>}
        </div>
      )}
      {podeAbrir && urna && (
        <p style={{ margin: 0, fontSize: 14 }}>
          {urna.candidatos.length} candidato(s) deferido(s) na urna. Eleitores: empregados CLT ativos da obra.
        </p>
      )}
      <FormRodape>
        {podeAbrir && (
          <Button appearance="primary" icon={<Play24Regular />} onClick={() => void abrir()} disabled={ocupado}>
            Abrir votação
          </Button>
        )}
        {aberta && (
          <>
            <Button appearance="primary" icon={<Open24Regular />} onClick={() => navegar(`/operacao/cipa/eleicao/${processoId}/urna`)}>
              Abrir tela da urna
            </Button>
            <Button icon={<Stop24Regular />} onClick={() => void encerrar()} disabled={ocupado}>
              Encerrar votação
            </Button>
          </>
        )}
      </FormRodape>
    </Card>
  );
}
