import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, EstadoVazio, FeedbackInline, PageHeader, StatusChip, type Tom } from '@ui';
import { ArrowLeft24Regular, Home24Regular } from '@fluentui/react-icons';
import { api, type AlojamentoResumo, type Obra } from '../../lib/api';

const tomStatus: Record<AlojamentoResumo['statusUltimaInspecao'], Tom> = {
  'em-dia': 'ok',
  atrasada: 'alerta',
  nunca: 'atencao',
};

function rotuloStatus(status: AlojamentoResumo['statusUltimaInspecao'], dias: number | null) {
  if (status === 'nunca') return 'Nunca inspecionado';
  if (status === 'atrasada') return `Atrasada · há ${dias} dias`;
  return `Em dia · há ${dias} dias`;
}

// Sub-aba "Alojamento" (Task 8, 2026-09-15): mesmo padrão obra → sub-itens de TrabalhadoresTab.tsx —
// grade de obras com resumo, clique abre a grade de alojamentos daquela obra. Diferente de
// TrabalhadoresTab, aqui os "sub-itens" também são cards (não uma DataTable), pois um alojamento
// não tem colunas tabulares ricas o bastante para justificar DataTable (regra dos 3 pilotos).
export function AlojamentoTab() {
  const navigate = useNavigate();
  const [obras, setObras] = useState<Obra[]>([]);
  const [alojamentos, setAlojamentos] = useState<AlojamentoResumo[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [obraSelecionadaId, setObraSelecionadaId] = useState<string | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [obs, alojs] = await Promise.all([api.obras.listar(), api.alojamentos.listar()]);
      setObras(obs);
      setAlojamentos(alojs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar alojamentos.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Task 9 (2026-09-15): clique no card do alojamento obtém (ou cria, se não houver uma em
  // andamento) a inspeção do dia via endpoint atômico (Task 7) e navega direto pro detalhe. Quando
  // já existia uma inspeção em andamento, leva um aviso em location.state pra InspecaoDetalhePage
  // mostrar que a inspeção foi retomada, não criada agora.
  async function abrirInspecao(alojamentoId: string) {
    try {
      const resultado = await api.alojamentos.obterOuCriarInspecaoAtual(alojamentoId);
      navigate(`/prevencao/inspecoes/${resultado.inspecaoId}`, {
        state: resultado.foiCriadaAgora
          ? undefined
          : {
              avisoRetomada: `Inspeção em andamento, criada em ${new Date(resultado.criadaEm).toLocaleString('pt-BR')}.`,
            },
      });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível abrir a inspeção deste alojamento.');
    }
  }

  const resumoPorObra = useMemo(
    () =>
      obras.map((obra) => {
        const doAlojamentos = alojamentos.filter((a) => a.obraId === obra.id);
        const totalMoradores = doAlojamentos.reduce((soma, a) => soma + a.totalMoradores, 0);
        const pendentes = doAlojamentos.filter((a) => a.statusUltimaInspecao !== 'em-dia').length;
        return { obra, alojamentos: doAlojamentos, totalMoradores, pendentes };
      }),
    [obras, alojamentos],
  );

  const obraAtual = obraSelecionadaId ? resumoPorObra.find((r) => r.obra.id === obraSelecionadaId) : null;

  function abrirObra(obraId: string) {
    setObraSelecionadaId(obraId);
  }

  function voltarParaObras() {
    setObraSelecionadaId(null);
  }

  if (obraSelecionadaId && obraAtual) {
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <PageHeader
          titulo={`Alojamento — ${obraAtual.obra.nome}`}
          filtros={
            <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={voltarParaObras}>
              Voltar às obras
            </Button>
          }
        />

        {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

        {obraAtual.alojamentos.length === 0 && (
          <Card>
            <EstadoVazio
              titulo="Nenhum alojamento cadastrado para esta obra ainda."
              descricao="Cadastre um alojamento para começar a controlar moradores e inspeções."
            />
          </Card>
        )}

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
          {obraAtual.alojamentos.map((alojamento) => (
            <Card key={alojamento.id}>
              <div
                role="button"
                tabIndex={0}
                onClick={() => abrirInspecao(alojamento.id)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' || e.key === ' ') abrirInspecao(alojamento.id);
                }}
                style={{ cursor: 'pointer', display: 'flex', flexDirection: 'column', gap: 8 }}
              >
                <div style={{ fontWeight: 600 }}>{alojamento.nome}</div>
                {alojamento.endereco && <div style={{ fontSize: 12 }}>{alojamento.endereco}</div>}
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, marginTop: 4 }}>
                  <Home24Regular />
                  <span style={{ fontWeight: 600 }}>{alojamento.totalMoradores}</span>
                  <span>{alojamento.totalMoradores === 1 ? 'morador' : 'moradores'}</span>
                </div>
                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                  <StatusChip tom={tomStatus[alojamento.statusUltimaInspecao]}>
                    {rotuloStatus(alojamento.statusUltimaInspecao, alojamento.diasDesdeUltimaInspecao)}
                  </StatusChip>
                </div>
              </div>
            </Card>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader titulo="Alojamento" subtitulo="Selecione uma obra para ver os alojamentos cadastrados nela." />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      {!carregandoLista && obras.length === 0 && (
        <Card>
          <EstadoVazio
            titulo="Nenhuma obra cadastrada ainda."
            descricao="Cadastre uma obra em Obras para depois vincular alojamentos a ela."
          />
        </Card>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: 16 }}>
        {resumoPorObra.map(({ obra, alojamentos: doAlojamentos, totalMoradores, pendentes }) => (
          <Card key={obra.id}>
            <div
              role="button"
              tabIndex={0}
              onClick={() => abrirObra(obra.id)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === ' ') abrirObra(obra.id);
              }}
              style={{ cursor: 'pointer', display: 'flex', flexDirection: 'column', gap: 8 }}
            >
              <div style={{ fontWeight: 600 }}>{obra.nome}</div>
              <div style={{ fontSize: 12 }}>{obra.codigo}</div>
              <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 12, marginTop: 4 }}>
                <Home24Regular />
                <span style={{ fontWeight: 600 }}>{doAlojamentos.length}</span>
                <span>{doAlojamentos.length === 1 ? 'alojamento' : 'alojamentos'}</span>
              </div>
              <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                <StatusChip tom="info">{totalMoradores} morador(es)</StatusChip>
                {doAlojamentos.length > 0 && (
                  <StatusChip tom={pendentes === 0 ? 'ok' : 'alerta'}>
                    {pendentes === 0 ? 'Todos em dia' : `${pendentes} pendente(s)`}
                  </StatusChip>
                )}
              </div>
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
