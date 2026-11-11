import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, DataTable, FeedbackInline, Field, Legenda, Select, StatusChip, type Coluna } from '@ui';
import { Add24Regular, ArrowRight20Regular } from '@fluentui/react-icons';
import { api, type Obra, type ResumoIntegracaoAtendimento, type StatusIntegracaoAtendimento } from '../../lib/api';

type Filtro = StatusIntegracaoAtendimento;

const dataBr = (iso?: string | null) => (iso ? iso.slice(0, 10).split('-').reverse().join('/') : '—');

// Aba "Integração" de Treinamentos (decisão do usuário, 11/11/2026): os atendimentos de integração em
// etapas da obra, em andamento ou concluídos. "Continuar" abre o atendimento na etapa em que parou e
// "Nova integração" abre a tela de início da obra escolhida.
export function IntegracoesTab() {
  const navigate = useNavigate();
  const [obras, setObras] = useState<Obra[]>([]);
  const [obraId, setObraId] = useState('');
  const [filtro, setFiltro] = useState<Filtro>('emAndamento');
  const [lista, setLista] = useState<ResumoIntegracaoAtendimento[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    api.obras.listar().then(setObras).catch(() => setObras([]));
  }, []);

  useEffect(() => {
    let ativo = true;
    setCarregando(true);
    setErro(null);
    api.integracoesAtendimento
      .listar({ obraId: obraId || undefined, status: filtro })
      .then((r) => ativo && setLista(r))
      .catch((e) => ativo && setErro(e instanceof Error ? e.message : 'Não foi possível carregar as integrações.'))
      .finally(() => ativo && setCarregando(false));
    return () => {
      ativo = false;
    };
  }, [obraId, filtro]);

  const abrirNova = () => navigate(`/treinamentos/integracao/nova${obraId ? `?obraId=${obraId}` : ''}`);

  const colunas: Coluna<ResumoIntegracaoAtendimento>[] = [
    { chave: 'numero', rotulo: 'Nº', render: (i) => <b>{i.numero}</b> },
    { chave: 'obra', rotulo: 'Obra', render: (i) => i.obraNome },
    {
      chave: 'situacao',
      rotulo: 'Situação',
      render: (i) =>
        i.status === 'concluido' ? (
          <StatusChip tom="ok">Concluída em {dataBr(i.concluidaEmUtc)}</StatusChip>
        ) : (
          <StatusChip tom="atencao">
            {i.etapaAtualOrdem ? `Etapa ${i.etapaAtualOrdem} de 5${i.etapaAtualTitulo ? ` · ${i.etapaAtualTitulo}` : ''}` : 'Em andamento'}
          </StatusChip>
        ),
    },
    { chave: 'pessoas', rotulo: 'Pessoas', render: (i) => i.participantes },
    { chave: 'inicio', rotulo: 'Início', render: (i) => dataBr(i.iniciadaEmUtc) },
    { chave: 'resp', rotulo: 'Responsável', render: (i) => i.responsavelNome ?? '—' },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Card
        titulo="Integração em etapas"
        subtitulo="NR-06, NR-12, NR-18, NR-35 e Ordem de Serviço, uma assinatura biométrica por curso."
        acoes={
          <Button appearance="primary" size="large" icon={<Add24Regular />} onClick={abrirNova}>
            Nova integração
          </Button>
        }
      >
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end', marginBottom: 12 }}>
          <Field label="Obra">
            <Select value={obraId} onChange={(_, d) => setObraId(d.value)} aria-label="Filtrar por obra">
              <option value="">Todas as obras</option>
              {obras.map((o) => (
                <option key={o.id} value={o.id}>{o.nome}</option>
              ))}
            </Select>
          </Field>
          <div role="group" aria-label="Situação" style={{ display: 'flex', gap: 8 }}>
            <Button appearance={filtro === 'emAndamento' ? 'primary' : 'secondary'} onClick={() => setFiltro('emAndamento')}>
              Em andamento
            </Button>
            <Button appearance={filtro === 'concluido' ? 'primary' : 'secondary'} onClick={() => setFiltro('concluido')}>
              Concluídos
            </Button>
          </div>
        </div>
        {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
        <DataTable
          aria-label="Integrações"
          colunas={colunas}
          linhas={lista}
          chaveLinha={(i) => i.id}
          carregando={carregando}
          vazio={{
            titulo: filtro === 'emAndamento' ? 'Nenhuma integração em andamento.' : 'Nenhuma integração concluída ainda.',
            acao: { rotulo: 'Nova integração', aoClicar: abrirNova },
          }}
          aoClicarLinha={(i) => navigate(`/treinamentos/integracao/${i.id}`)}
          acoesLinha={(i) => (
            <Button size="small" icon={<ArrowRight20Regular />} onClick={() => navigate(`/treinamentos/integracao/${i.id}`)}>
              {i.status === 'emAndamento' ? 'Continuar' : 'Abrir'}
            </Button>
          )}
        />
      </Card>
      <Legenda>Os certificados de cada NR ficam no perfil de cada pessoa, em Treinamentos.</Legenda>
    </div>
  );
}
