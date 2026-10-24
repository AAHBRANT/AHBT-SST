import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Avatar, PainelLateral, DataTable, StatusChip, FeedbackInline, Field, Input, type Coluna, type Tom } from '@ui';
import { Search24Regular } from '@fluentui/react-icons';
import {
  api,
  resultadoAsoLabel,
  ResultadoAso,
  type Aso,
  type Funcao,
  type Obra,
  type Trabalhador,
} from '../../lib/api';

// Situação clínica do ASO mais recente do trabalhador — mesma leitura direta do
// campo Aso.resultadoStatus usada em PessoasDashboardTab (sem reclassificar por
// prazo de validade, que é regra própria daquele dashboard).
const tomAso: Record<number, Tom> = {
  [ResultadoAso.Apto]: 'ok',
  [ResultadoAso.AptoComRestricao]: 'atencao',
  [ResultadoAso.Inapto]: 'alerta',
};

// Busca rápida de funcionários, aberta de qualquer tela pela topbar (AppShell). Consumidora de
// PainelLateral (spec §3: é o componente que absorve exatamente este estilo de gaveta) — a lista
// interna vira DataTable, mesmo padrão já usado em TrabalhadoresTab, para as duas telas lerem igual.
// Interface pública inalterada: `aberta`/`aoFechar`/`buscaInicial` continuam os mesmos props que
// AppShell.tsx já consome.
export function TrabalhadoresGaveta({
  aberta,
  aoFechar,
  buscaInicial,
}: {
  aberta: boolean;
  aoFechar: () => void;
  /** Ex.: nome da obra, para abrir a gaveta já filtrada a partir de outra tela. */
  buscaInicial?: string;
}) {
  const navigate = useNavigate();

  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [carregando, setCarregando] = useState(false);
  const [carregou, setCarregou] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [busca, setBusca] = useState('');

  // Carrega sob demanda, só na primeira vez que a gaveta é aberta.
  useEffect(() => {
    if (!aberta || carregou) return;
    setCarregando(true);
    setErro(null);
    Promise.all([api.trabalhadores.listar(), api.obras.listar(), api.funcoes.listar(), api.asos.listar()])
      .then(([listaTrabalhadores, listaObras, listaFuncoes, listaAsos]) => {
        setTrabalhadores(listaTrabalhadores);
        setObras(listaObras);
        setFuncoes(listaFuncoes);
        setAsos(listaAsos);
        setCarregou(true);
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar funcionários.'))
      .finally(() => setCarregando(false));
  }, [aberta, carregou]);

  useEffect(() => {
    if (aberta && buscaInicial) setBusca(buscaInicial);
  }, [aberta, buscaInicial]);

  const asoMaisRecentePorTrabalhador = useMemo(() => {
    const mapa = new Map<string, Aso>();
    for (const aso of asos) {
      const atual = mapa.get(aso.trabalhadorId);
      if (!atual || aso.dataExame > atual.dataExame) mapa.set(aso.trabalhadorId, aso);
    }
    return mapa;
  }, [asos]);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }
  function nomeFuncao(id: string) {
    return funcoes.find((f) => f.id === id)?.nome ?? id;
  }

  const filtrados = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    if (!termo) return trabalhadores;
    return trabalhadores.filter((t) =>
      `${t.nome} ${nomeFuncao(t.funcaoId)} ${nomeObra(t.obraId)} ${t.matricula ?? ''}`.toLowerCase().includes(termo),
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadores, busca, obras, funcoes]);

  function aoSelecionarTrabalhador(trabalhador: Trabalhador) {
    aoFechar();
    navigate(`/pessoas/${trabalhador.id}`);
  }

  const colunas: Coluna<Trabalhador>[] = [
    {
      chave: 'trabalhador',
      rotulo: 'Funcionário',
      render: (t) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <Avatar name={t.nome} color="colorful" size={32} />
          <div style={{ minWidth: 0 }}>
            <div style={{ fontWeight: 600, fontSize: 13 }}>{t.nome}</div>
            <div style={{ fontSize: 12 }}>
              {nomeFuncao(t.funcaoId)} · {nomeObra(t.obraId)}
            </div>
          </div>
        </div>
      ),
    },
    {
      chave: 'situacao',
      rotulo: 'Situação',
      render: (t) => {
        const aso = asoMaisRecentePorTrabalhador.get(t.id);
        return (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: 4 }}>
            {t.matricula && <span style={{ fontSize: 11 }}>Mat. {t.matricula}</span>}
            <StatusChip tom={aso ? tomAso[aso.resultadoStatus] ?? 'info' : 'neutro'}>
              {aso ? resultadoAsoLabel[aso.resultadoStatus] : 'Sem ASO'}
            </StatusChip>
          </div>
        );
      },
    },
  ];

  return (
    <PainelLateral
      aberto={aberta}
      aoFechar={aoFechar}
      titulo="Funcionários"
      subtitulo={carregou ? `${trabalhadores.length} cadastrados` : carregando ? 'Carregando...' : undefined}
    >
      <Field label="Buscar por nome, função ou obra" style={{ marginBottom: 12 }}>
        <Input contentBefore={<Search24Regular />} value={busca} onChange={(_, d) => setBusca(d.value)} />
      </Field>

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <DataTable
        aria-label="Funcionários"
        densidade="compacta"
        colunas={colunas}
        linhas={filtrados}
        chaveLinha={(t) => t.id}
        carregando={carregando}
        vazio={{ titulo: 'Nenhum funcionário encontrado.', variante: 'sem-resultado' }}
        aoClicarLinha={aoSelecionarTrabalhador}
      />
    </PainelLateral>
  );
}
