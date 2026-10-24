import { useEffect, useMemo, useState } from 'react';
import { DataTable, EstadoVazio, FeedbackInline, Input, Text, tokensUi, type Coluna } from '@ui';
import { api, type ExameFuncaoObra } from '../../lib/api';
import { asosDoExame, nomeExame, rotuloPeriodicidade } from './formatacao';

// Aba "Exames por função" do PCMSO (aprovada no mockup de 09/10/2026): o quadro de exames vira uma
// grade função × exame com a periodicidade do periódico; clicar na função abre em quais ASOs cada
// exame é pedido.
interface LinhaFuncao {
  funcaoId: string;
  funcaoNome: string;
  exames: Map<string, ExameFuncaoObra>;
}

// Ordem das colunas: exames comuns primeiro, específicos (ex.: ácido hipúrico) no fim.
const ORDEM_EXAMES = [
  'EXAME CLINICO',
  'AUDIOMETRIA TONAL',
  'ACUIDADE VISUAL',
  'AVALIACAO PSICOSSOCIAL',
  'ELETROCARDIOGRAMA',
  'ELETROENCEFALOGRAMA',
  'ESPIROMETRIA',
  'GLICEMIA EM JEJUM',
  'HEMOGRAMA COMPLETO',
  'RAIO-X DE TORAX PADRAO OIT',
];

const ROTULO_CURTO: Record<string, string> = {
  'EXAME CLINICO': 'Clínico',
  'AUDIOMETRIA TONAL': 'Audiometria',
  'ACUIDADE VISUAL': 'Acuidade',
  'AVALIACAO PSICOSSOCIAL': 'Psicossocial',
  ELETROCARDIOGRAMA: 'ECG',
  ELETROENCEFALOGRAMA: 'EEG',
  ESPIROMETRIA: 'Espirometria',
  'GLICEMIA EM JEJUM': 'Glicemia',
  'HEMOGRAMA COMPLETO': 'Hemograma',
  'RAIO-X DE TORAX PADRAO OIT': 'RX OIT',
  'ACIDO HIPURICO': 'Ác. hipúrico',
  'ACIDO METIL HIPURICO': 'Ác. metil-hipúrico',
};

function corPeriodicidade(meses: number | null) {
  if (meses === 12) return tokensUi.status.atencao;
  if (meses === 24) return tokensUi.status.ok;
  if (meses != null && meses < 12) return tokensUi.status.alerta;
  return tokensUi.status.neutro;
}

function posicao(e: ExameFuncaoObra) {
  const i = ORDEM_EXAMES.indexOf(e.exame);
  return i === -1 ? ORDEM_EXAMES.length : i;
}

export function ExamesFuncaoTab({
  obraId,
  pcmsoId,
  pendenteValidacao,
}: {
  obraId: string;
  pcmsoId: string;
  pendenteValidacao: boolean;
}) {
  const [exames, setExames] = useState<ExameFuncaoObra[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [busca, setBusca] = useState('');
  const [aberta, setAberta] = useState<string | null>(null);

  useEffect(() => {
    let ativo = true;
    setExames(null);
    setErro(null);
    api.estruturaSst
      .listarExamesFuncao(obraId)
      .then((dados) => {
        if (ativo) setExames(dados.filter((e) => e.pcmsoDetalheId == null || e.pcmsoDetalheId === pcmsoId));
      })
      .catch((e: unknown) => {
        if (ativo) setErro(e instanceof Error ? e.message : 'Não foi possível carregar os exames.');
      });
    return () => {
      ativo = false;
    };
  }, [obraId, pcmsoId]);

  const { linhas, colunasExame } = useMemo(() => {
    const porFuncao = new Map<string, LinhaFuncao>();
    const nomes = new Set<string>();
    (exames ?? []).forEach((e) => {
      nomes.add(e.exame);
      const linha = porFuncao.get(e.funcaoId) ?? { funcaoId: e.funcaoId, funcaoNome: e.funcaoNome ?? '—', exames: new Map() };
      linha.exames.set(e.exame, e);
      porFuncao.set(e.funcaoId, linha);
    });
    const ordenados = [
      ...ORDEM_EXAMES.filter((n) => nomes.has(n)),
      ...[...nomes].filter((n) => !ORDEM_EXAMES.includes(n)).sort(),
    ];
    return {
      linhas: [...porFuncao.values()].sort((a, b) => a.funcaoNome.localeCompare(b.funcaoNome, 'pt-BR')),
      colunasExame: ordenados,
    };
  }, [exames]);

  if (erro) return <FeedbackInline tom="erro">{erro}</FeedbackInline>;
  if (exames && exames.length === 0) {
    return (
      <EstadoVazio
        titulo="Nenhum exame por função cadastrado para a obra deste PCMSO."
        descricao="O quadro de exames entra pela importação da estrutura do PGR e do PCMSO da obra."
      />
    );
  }

  const termo = busca.trim().toLowerCase();
  const filtradas = termo ? linhas.filter((l) => l.funcaoNome.toLowerCase().includes(termo)) : linhas;

  const colunas: Coluna<LinhaFuncao>[] = [
    { chave: 'funcao', rotulo: 'Função', render: (l) => <Text weight="semibold">{l.funcaoNome}</Text> },
    ...colunasExame.map<Coluna<LinhaFuncao>>((nome) => ({
      chave: nome,
      rotulo: ROTULO_CURTO[nome] ?? nomeExame(nome),
      render: (l) => {
        const e = l.exames.get(nome);
        if (!e) return '';
        const cor = corPeriodicidade(e.periodicidadeMeses);
        return (
          <span
            title={`${nomeExame(e.exame)} · ${rotuloPeriodicidade(e.periodicidadeMeses)}`}
            style={{
              display: 'inline-block',
              minWidth: 36,
              textAlign: 'center',
              borderRadius: 4,
              padding: '1px 6px',
              fontSize: 12,
              fontWeight: 600,
              color: cor.tinta,
              background: cor.fundo,
            }}
          >
            {e.periodicidadeMeses != null ? `${e.periodicidadeMeses}m` : '—'}
          </span>
        );
      },
    })),
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md }}>
      {pendenteValidacao && (
        <FeedbackInline tom="aviso">
          Quadro importado do PCMSO e ainda não validado pela médica coordenadora. Confira as pendências de validação
          técnica no plano de ação do PGR.
        </FeedbackInline>
      )}
      <div style={{ display: 'flex', gap: tokensUi.espaco.md, alignItems: 'center', flexWrap: 'wrap' }}>
        <Input placeholder="Buscar função…" value={busca} onChange={(_, d) => setBusca(d.value)} aria-label="Buscar função" />
        <Text size={200}>12m = anual · 24m = bienal · célula vazia = exame não previsto para a função</Text>
      </div>
      <DataTable
        aria-label="Exames por função"
        densidade="compacta"
        cabecalhoFixo
        colunas={colunas}
        linhas={filtradas}
        carregando={exames === null}
        chaveLinha={(l) => l.funcaoId}
        aoClicarLinha={(l) => setAberta((atual) => (atual === l.funcaoId ? null : l.funcaoId))}
        vazio={{ titulo: 'Nenhuma função encontrada.', variante: 'sem-resultado' }}
        expansivel={{
          aberta: (l) => aberta === l.funcaoId,
          render: (l) => <TabelaExamesFuncao exames={[...l.exames.values()]} />,
        }}
      />
    </div>
  );
}

export function TabelaExamesFuncao({ exames }: { exames: ExameFuncaoObra[] }) {
  const colunas: Coluna<ExameFuncaoObra>[] = [
    {
      chave: 'exame',
      rotulo: 'Exame',
      render: (e) => (
        <div>
          {nomeExame(e.exame)}
          {e.codigoExame && <div style={{ fontSize: 12, opacity: 0.7 }}>Código eSocial {e.codigoExame}</div>}
        </div>
      ),
    },
    { chave: 'periodico', rotulo: 'Periódico', render: (e) => (e.periodico ? rotuloPeriodicidade(e.periodicidadeMeses) : '—') },
    { chave: 'asos', rotulo: 'Entra no ASO', render: (e) => asosDoExame(e) },
  ];
  return (
    <DataTable
      aria-label="Exames da função"
      densidade="compacta"
      colunas={colunas}
      linhas={[...exames].sort((a, b) => posicao(a) - posicao(b) || a.exame.localeCompare(b.exame))}
      chaveLinha={(e) => e.id}
      vazio={{ titulo: 'Nenhum exame previsto para esta função.' }}
    />
  );
}
