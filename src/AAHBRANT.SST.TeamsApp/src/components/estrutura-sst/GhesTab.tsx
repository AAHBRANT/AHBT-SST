import { useEffect, useState } from 'react';
import { DataTable, EstadoVazio, FeedbackInline, StatusChip, Text, tokensUi, type Coluna } from '@ui';
import { api, nivelRiscoLabel, type Ghe, type GheRisco } from '../../lib/api';
import { extrairControle, tomPorNivel } from './formatacao';

// Aba "GHE" do PGR (aprovada no mockup de 09/10/2026): grupos homogêneos da obra com as funções
// expostas e os riscos de cada um, do mais grave ao mais leve. Só leitura — a estrutura vem da
// importação do PGR (POST /api/obras/{id}/estrutura-sst/importar).
const colunasRisco: Coluna<GheRisco>[] = [
  {
    chave: 'perigo',
    rotulo: 'Perigo',
    render: (r) => (
      <div>
        <Text weight="semibold">{r.perigo ?? '—'}</Text>
        {r.tipoAgente && <div style={{ fontSize: 12, opacity: 0.75 }}>{r.tipoAgente}</div>}
      </div>
    ),
  },
  { chave: 'ps', rotulo: 'P × S', render: (r) => `${r.probabilidade} × ${r.severidade}` },
  {
    chave: 'nivel',
    rotulo: 'Nível',
    render: (r) => <StatusChip tom={tomPorNivel[r.nivelRisco] ?? 'neutro'}>{nivelRiscoLabel[r.nivelRisco]}</StatusChip>,
  },
  { chave: 'epi', rotulo: 'EPI', render: (r) => extrairControle(r.controlesExistentes, 'EPI') },
  { chave: 'epc', rotulo: 'EPC', render: (r) => extrairControle(r.controlesExistentes, 'EPC') },
];

function ResumoNiveis({ riscos }: { riscos: GheRisco[] }) {
  const contagem = new Map<number, number>();
  riscos.forEach((r) => contagem.set(r.nivelRisco, (contagem.get(r.nivelRisco) ?? 0) + 1));
  return (
    <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
      {[...contagem.entries()]
        .sort(([a], [b]) => b - a)
        .map(([nivel, qtd]) => (
          <StatusChip key={nivel} tom={tomPorNivel[nivel] ?? 'neutro'}>
            {qtd} {nivelRiscoLabel[nivel].toLowerCase()}
          </StatusChip>
        ))}
    </div>
  );
}

const colunasGhe: Coluna<Ghe>[] = [
  { chave: 'numero', rotulo: 'GHE', render: (g) => <Text weight="semibold">GHE {String(g.numero).padStart(2, '0')}</Text> },
  {
    chave: 'funcoes',
    rotulo: 'Funções (expostos no PGR)',
    render: (g) =>
      g.funcoes
        .map((f) => `${f.funcaoNome ?? '—'}${f.quantidadeExpostos ? ` (${f.quantidadeExpostos})` : ''}`)
        .join(', '),
  },
  { chave: 'setor', rotulo: 'Setor', render: (g) => g.setor ?? '—' },
  { chave: 'critica', rotulo: 'Atividade crítica', render: (g) => g.atividadesCriticas ?? '—' },
  { chave: 'riscos', rotulo: 'Riscos', render: (g) => <ResumoNiveis riscos={g.riscos} /> },
];

export function GhesTab({ obraId }: { obraId: string }) {
  const [ghes, setGhes] = useState<Ghe[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [abertos, setAbertos] = useState<Set<string>>(new Set());

  useEffect(() => {
    let ativo = true;
    setGhes(null);
    setErro(null);
    api.estruturaSst
      .listarGhes(obraId)
      .then((dados) => ativo && setGhes(dados))
      .catch((e: unknown) => ativo && setErro(e instanceof Error ? e.message : 'Não foi possível carregar os GHE.'));
    return () => {
      ativo = false;
    };
  }, [obraId]);

  if (erro) return <FeedbackInline tom="erro">{erro}</FeedbackInline>;
  if (ghes && ghes.length === 0) {
    return (
      <EstadoVazio
        titulo="Nenhum GHE cadastrado para a obra deste PGR."
        descricao="Os GHE entram pela importação da estrutura do PGR e do PCMSO da obra."
      />
    );
  }

  function alternar(g: Ghe) {
    setAbertos((atual) => {
      const novo = new Set(atual);
      if (novo.has(g.id)) novo.delete(g.id);
      else novo.add(g.id);
      return novo;
    });
  }

  return (
    <DataTable
      aria-label="Grupos homogêneos de exposição"
      colunas={colunasGhe}
      linhas={ghes ?? []}
      carregando={ghes === null}
      chaveLinha={(g) => g.id}
      aoClicarLinha={alternar}
      expansivel={{
        aberta: (g) => abertos.has(g.id),
        render: (g) => (
          <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.sm, padding: tokensUi.espaco.sm }}>
            {(g.descricaoAmbiente || g.jornadaTrabalho) && (
              <Text size={200}>
                {g.descricaoAmbiente && <>Ambiente: {g.descricaoAmbiente}</>}
                {g.descricaoAmbiente && g.jornadaTrabalho && ' · '}
                {g.jornadaTrabalho && <>Jornada: {g.jornadaTrabalho}</>}
              </Text>
            )}
            <DataTable
              aria-label={`Riscos do GHE ${g.numero}`}
              densidade="compacta"
              colunas={colunasRisco}
              linhas={g.riscos}
              chaveLinha={(r) => r.id}
              vazio={{ titulo: 'Nenhum risco cadastrado para este GHE.' }}
            />
          </div>
        ),
      }}
    />
  );
}
