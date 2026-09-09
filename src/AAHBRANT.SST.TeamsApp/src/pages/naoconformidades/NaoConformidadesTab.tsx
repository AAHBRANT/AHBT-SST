import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Campo,
  CampoData,
  Card,
  DataTable,
  Field,
  FormGrid,
  FormSection,
  FeedbackInline,
  Input,
  PageHeader,
  PainelLateral,
  Select,
  StatusChip,
  type Coluna,
  type Tom,
} from '@ui';
import { AddCircle24Regular } from '@fluentui/react-icons';
import {
  api,
  origemNaoConformidadeLabel,
  statusNaoConformidadeLabel,
  StatusNaoConformidade,
  type Atividade,
  type NaoConformidade,
  type NovaNaoConformidade,
  type Risco,
  type Usuario,
} from '../../lib/api';

function novaInicial(): NovaNaoConformidade {
  return {
    origemDeteccao: 1,
    requisitoRelacionado: '',
    descricao: '',
    local: '',
    atividadeId: '',
    riscoId: '',
    responsavelUsuarioId: '',
    prazo: '',
  };
}

// Tom do chip por estado do fluxo — mesmo mapa de NaoConformidadeDetalhePage.tsx (interface
// importante entre lista e detalhe: precisa ser o MESMO mapeamento, senão o mesmo status aparece
// com cores diferentes ao navegar de uma tela pra outra). EmAnalise (6) não está aqui de propósito:
// o fluxo atual do backend não emite esse estado, então cai no 'neutro' do fallback.
const tomStatus: Record<number, Tom> = {
  [StatusNaoConformidade.Aberta]: 'neutro',
  [StatusNaoConformidade.Enviada]: 'info',
  [StatusNaoConformidade.Devolvida]: 'alerta',
  [StatusNaoConformidade.EmAndamento]: 'atencao',
  [StatusNaoConformidade.AguardandoValidacao]: 'info',
  [StatusNaoConformidade.Encerrada]: 'ok',
};

// Onda 2 Task 15 (camada ui/): lista de não conformidades — mesmo padrão já usado em
// TrabalhadoresTab.tsx/AtividadesTab.tsx (golden rule, spec §5.1): o formulário de criação, que
// empurrava a tabela pra baixo, sai para um PainelLateral com erro PRÓPRIO (regra dos 3 pilotos:
// erro de painel nunca reaproveita o erro de carga da lista). A linha inteira já navega para o
// detalhe via aoClicarLinha — o antigo <ChevronRight24Regular> era só um espelho redundante disso
// e sai (mesma regra do Guia de conversão, seção 1).
export function NaoConformidadesTab() {
  const navigate = useNavigate();
  const [naoConformidades, setNaoConformidades] = useState<NaoConformidade[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [riscos, setRiscos] = useState<Risco[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [nova, setNova] = useState<NovaNaoConformidade>(novaInicial());
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [listaNc, listaAtividades, listaRiscos, listaUsuarios] = await Promise.all([
        api.naoConformidades.listar(),
        api.atividades.listar(),
        api.riscos.listar(),
        api.usuarios.listar(),
      ]);
      setNaoConformidades(listaNc);
      setAtividades(listaAtividades);
      setRiscos(listaRiscos);
      setUsuarios(listaUsuarios);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar não conformidades.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Todo caminho de fechar o painel limpa o formulário e o erro dele — senão reabrir mostra
  // rascunho e mensagem de uma tentativa anterior (regra dos 3 pilotos).
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNova(novaInicial());
  }

  async function criar() {
    if (!nova.descricao.trim()) {
      setErroPainel('Informe a descrição da não conformidade.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.naoConformidades.criar({
        ...nova,
        requisitoRelacionado: nova.requisitoRelacionado || null,
        local: nova.local || null,
        atividadeId: nova.atividadeId || null,
        riscoId: nova.riscoId || null,
        responsavelUsuarioId: nova.responsavelUsuarioId || null,
        prazo: nova.prazo || null,
      });
      setNova(novaInicial());
      setPainelAberto(false);
      await carregar();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar não conformidade.');
    } finally {
      setCarregando(false);
    }
  }

  const colunas: Coluna<NaoConformidade>[] = [
    { chave: 'origem', rotulo: 'Origem', render: (nc) => origemNaoConformidadeLabel[nc.origemDeteccao] },
    { chave: 'descricao', rotulo: 'Descrição' },
    { chave: 'atividade', rotulo: 'Atividade', render: (nc) => nc.atividadeNome ?? '—' },
    { chave: 'responsavel', rotulo: 'Responsável', render: (nc) => nc.responsavelUsuarioNome ?? '—' },
    { chave: 'prazo', rotulo: 'Prazo', largura: '108px', render: (nc) => nc.prazo?.slice(0, 10) ?? '—' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (nc) => <StatusChip tom={tomStatus[nc.status] ?? 'neutro'}>{statusNaoConformidadeLabel[nc.status]}</StatusChip>,
    },
  ];

  return (
    <div>
      <PageHeader
        titulo="Não conformidades"
        acoes={
          <Button appearance="primary" icon={<AddCircle24Regular />} onClick={() => setPainelAberto(true)}>
            Nova não conformidade
          </Button>
        }
      />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card>
        <DataTable
          aria-label="Não conformidades cadastradas"
          colunas={colunas}
          linhas={naoConformidades}
          chaveLinha={(nc) => nc.id}
          carregando={carregandoLista}
          aoClicarLinha={(nc) => navigate(`/nao-conformidades/${nc.id}`)}
          vazio={{
            titulo: 'Nenhuma não conformidade registrada ainda.',
            descricao: 'Registre a primeira não conformidade para começar.',
            acao: { rotulo: 'Nova não conformidade', aoClicar: () => setPainelAberto(true) },
          }}
        />
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Nova não conformidade"
        largura="lg"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Registrar
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}

        <FormSection titulo="Dados da Não Conformidade" numero={1} primeira>
          <FormGrid>
            <Campo span={3}>
              <Field label="Origem">
                <Select
                  value={String(nova.origemDeteccao)}
                  onChange={(_, d) => setNova({ ...nova, origemDeteccao: Number(d.value) })}
                >
                  {Object.entries(origemNaoConformidadeLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Requisito relacionado">
                <Input
                  value={nova.requisitoRelacionado ?? ''}
                  onChange={(_, d) => setNova({ ...nova, requisitoRelacionado: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={5}>
              <Field label="Descrição" required>
                <Input value={nova.descricao} onChange={(_, d) => setNova({ ...nova, descricao: d.value })} />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Local">
                <Input value={nova.local ?? ''} onChange={(_, d) => setNova({ ...nova, local: d.value })} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Atividade">
                <Select
                  value={nova.atividadeId ?? ''}
                  onChange={(_, d) => setNova({ ...nova, atividadeId: d.value })}
                >
                  <option value="">Nenhuma</option>
                  {atividades.map((atividade) => (
                    <option key={atividade.id} value={atividade.id}>
                      {atividade.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Risco associado">
                <Select value={nova.riscoId ?? ''} onChange={(_, d) => setNova({ ...nova, riscoId: d.value })}>
                  <option value="">Nenhum</option>
                  {riscos.map((risco) => (
                    <option key={risco.id} value={risco.id}>
                      {risco.ambiente || risco.id}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Responsável">
                <Select
                  value={nova.responsavelUsuarioId ?? ''}
                  onChange={(_, d) => setNova({ ...nova, responsavelUsuarioId: d.value })}
                >
                  <option value="">Nenhum</option>
                  {usuarios.map((usuario) => (
                    <option key={usuario.id} value={usuario.id}>
                      {usuario.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Prazo">
                <CampoData value={nova.prazo ?? ''} onChange={(_, d) => setNova({ ...nova, prazo: d.value })} />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
      </PainelLateral>
    </div>
  );
}
