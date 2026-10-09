import { useEffect, useState } from 'react';
import { Button, Card, DataTable, FeedbackInline, StatusChip, Text, type Coluna } from '@ui';
import { api, nivelRiscoLabel, type ExameFuncaoObra, type Ghe, type GheRisco } from '../../lib/api';
import { TabelaExamesFuncao } from './ExamesFuncaoTab';
import { extrairControle, tomPorNivel } from './formatacao';

// Cards do perfil do trabalhador (aprovados no mockup de 09/10/2026): GHE e exames descobertos pela
// FUNÇÃO do trabalhador na obra dele, sem vínculo manual. Não substituem o card "Riscos expostos
// (PGR)" (vínculos manuais), que continua na aba. Sem GHE/PCMSO importado para a obra, não aparecem.
const LIMITE_RISCOS = 6;

const colunasRisco: Coluna<GheRisco>[] = [
  { chave: 'perigo', rotulo: 'Perigo', render: (r) => <Text weight="semibold">{r.perigo ?? '—'}</Text> },
  { chave: 'ps', rotulo: 'P × S', render: (r) => `${r.probabilidade} × ${r.severidade}` },
  {
    chave: 'nivel',
    rotulo: 'Nível',
    render: (r) => <StatusChip tom={tomPorNivel[r.nivelRisco] ?? 'neutro'}>{nivelRiscoLabel[r.nivelRisco]}</StatusChip>,
  },
  { chave: 'epi', rotulo: 'EPI', render: (r) => extrairControle(r.controlesExistentes, 'EPI') },
];

export function GheDaFuncaoCard({ obraId, funcaoId }: { obraId: string; funcaoId: string }) {
  const [ghes, setGhes] = useState<Ghe[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [todos, setTodos] = useState(false);

  useEffect(() => {
    let ativo = true;
    setGhes(null);
    setErro(null);
    api.estruturaSst
      .listarGhes(obraId)
      .then((dados) => {
        if (ativo) setGhes(dados);
      })
      .catch((e: unknown) => {
        if (ativo) setErro(e instanceof Error ? e.message : 'Não foi possível carregar o GHE da função.');
      });
    return () => {
      ativo = false;
    };
  }, [obraId]);

  if (erro) return <FeedbackInline tom="erro">{erro}</FeedbackInline>;
  const ghe = ghes?.find((g) => g.funcoes.some((f) => f.funcaoId === funcaoId));
  if (!ghe) return null;

  const numero = String(ghe.numero).padStart(2, '0');
  const visiveis = todos ? ghe.riscos : ghe.riscos.slice(0, LIMITE_RISCOS);
  const subtitulo = [ghe.setor, ghe.atividadesCriticas].filter(Boolean).join(' · ');

  return (
    <Card
      titulo={`GHE ${numero} da função · ${ghe.riscos.length} riscos`}
      subtitulo={subtitulo || undefined}
      acoes={
        ghe.riscos.length > LIMITE_RISCOS ? (
          <Button appearance="subtle" onClick={() => setTodos((v) => !v)}>
            {todos ? 'Mostrar menos' : `Ver todos os ${ghe.riscos.length}`}
          </Button>
        ) : undefined
      }
    >
      <DataTable
        aria-label={`Riscos do GHE ${numero}`}
        densidade="compacta"
        colunas={colunasRisco}
        linhas={visiveis}
        chaveLinha={(r) => r.id}
        vazio={{ titulo: 'Nenhum risco cadastrado para este GHE.' }}
      />
    </Card>
  );
}

export function ExamesDaFuncaoCard({ obraId, funcaoId }: { obraId: string; funcaoId: string }) {
  const [exames, setExames] = useState<ExameFuncaoObra[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    let ativo = true;
    setExames(null);
    setErro(null);
    api.estruturaSst
      .listarExamesFuncao(obraId, funcaoId)
      .then((dados) => {
        if (ativo) setExames(dados);
      })
      .catch((e: unknown) => {
        if (ativo) setErro(e instanceof Error ? e.message : 'Não foi possível carregar os exames da função.');
      });
    return () => {
      ativo = false;
    };
  }, [obraId, funcaoId]);

  if (erro) return <FeedbackInline tom="erro">{erro}</FeedbackInline>;
  if (!exames || exames.length === 0) return null;

  return (
    <Card titulo="Exames previstos pela função (PCMSO)">
      <TabelaExamesFuncao exames={exames} />
    </Card>
  );
}
