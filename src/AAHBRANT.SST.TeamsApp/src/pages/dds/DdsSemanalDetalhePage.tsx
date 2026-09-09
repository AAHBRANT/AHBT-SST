import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Button,
  Field,
  Input,
  Select,
  Card,
  PageHeader,
  StatusChip,
  FeedbackInline,
  Carregando,
  Legenda,
  FormSection,
  FormGrid,
  Campo,
  FormRodape,
  ChipCheckboxGroup,
  type Tom,
} from '@ui';
import { Add24Regular, ArrowDownload24Regular, ChevronRight24Regular, LockClosed24Regular } from '@fluentui/react-icons';
import {
  api,
  StatusDds,
  statusDdsLabel,
  StatusDdsSemanal,
  statusDdsSemanalLabel,
  tipoDdsSemanalLabel,
  TipoDdsSemanal,
  type Atividade,
  type CatalogoTemaDds,
  type DdsSemanalDetalhe,
} from '../../lib/api';

const NOMES_DIAS = ['Segunda-feira', 'Terça-feira', 'Quarta-feira', 'Quinta-feira', 'Sexta-feira'];

function novoDiaVazio() {
  return { atividadesIds: [] as string[], catalogoTemaDdsId: '' };
}

const tomStatusSemanal: Record<number, Tom> = {
  [StatusDdsSemanal.EmAndamento]: 'info',
  [StatusDdsSemanal.Concluida]: 'ok',
};

const tomStatusDia: Record<number, Tom> = {
  [StatusDds.EmAndamento]: 'atencao',
  [StatusDds.Concluido]: 'ok',
};

// Semana (contêiner) do DDS reformulado (31/08) — cada um dos 5 dias úteis é um registro diário
// próprio (DdsDetalhePage), feito e assinado no seu próprio dia. Esta tela mostra os 5 slots
// Seg-Sex: dia já registrado abre o detalhe; dia em aberto mostra o formulário de criação do
// registro daquele dia (seleção de atividades + origem do tema, ver CriarDdsCommand).
// Onda 2 (Task 14): conversões 2 (estilos.card/toolbar → Card + PageHeader), 3 (Text size=500 →
// PageHeader.titulo) e 4 (erro → FeedbackInline). Nada aqui usava <Table>/Badge de status com cor
// própria além do status geral (conversão 5 aplicada de passagem nos dois status). O checklist de
// atividades do dia usava useCheckboxChipStyles cru — vira ChipCheckboxGroup (o próprio componente
// já cita DDS como consumidor no seu comentário de origem). Sem WorkflowActions: a única transição
// real (encerrar semana) tem um único item condicional, sem ganho sobre um botão simples no rodapé
// do formulário — mesmo julgamento já registrado para PcmsoDetalhePage.tsx (Task 12).
export function DdsSemanalDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [detalhe, setDetalhe] = useState<DdsSemanalDetalhe | null>(null);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [catalogoTemas, setCatalogoTemas] = useState<CatalogoTemaDds[]>([]);
  const [diaEmCriacao, setDiaEmCriacao] = useState<string | null>(null);
  const [novoDia, setNovoDia] = useState(novoDiaVazio());
  const [responsavelTerceirizadaNome, setResponsavelTerceirizadaNome] = useState('');
  const [responsavelTerceirizadaFuncao, setResponsavelTerceirizadaFuncao] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoPdf, setBaixandoPdf] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const det = await api.ddsSemanal.obterDetalhe(id);
      setDetalhe(det);
      const [listaAtividades, listaTemas] = await Promise.all([
        api.atividades.listar(det.semanal.obraId),
        api.catalogoTemasDds.listar(),
      ]);
      setAtividades(listaAtividades);
      setCatalogoTemas(listaTemas);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a semana de DDS.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function abrirCriacaoDia(data: string) {
    setDiaEmCriacao(data);
    setNovoDia(novoDiaVazio());
  }

  async function criarRegistroDia() {
    if (!id || !diaEmCriacao || novoDia.atividadesIds.length === 0) {
      setErro('Selecione ao menos uma atividade do dia.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.dds.criar({
        ddsSemanalId: id,
        atividadesIds: novoDia.atividadesIds,
        data: diaEmCriacao,
        catalogoTemaDdsId: novoDia.catalogoTemaDdsId || null,
      });
      setDiaEmCriacao(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar o registro do dia.');
    } finally {
      setProcessando(false);
    }
  }

  async function encerrarSemana() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.ddsSemanal.encerrar(id, {
        responsavelEmpresaTerceirizadaNome: responsavelTerceirizadaNome || null,
        responsavelEmpresaTerceirizadaFuncao: responsavelTerceirizadaFuncao || null,
      });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar a semana.');
    } finally {
      setProcessando(false);
    }
  }

  async function baixarPdf() {
    if (!id) return;
    try {
      setBaixandoPdf(true);
      setErro(null);
      const blob = await api.ddsSemanal.baixarPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `dds-semanal-${id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o PDF da semana.');
    } finally {
      setBaixandoPdf(false);
    }
  }

  if (!id) return <FeedbackInline tom="erro">Semana de DDS não encontrada.</FeedbackInline>;

  const semanal = detalhe?.semanal;

  // Erro na carga inicial precisa aparecer aqui: sem isso o skeleton ficaria para sempre (mesmo
  // padrão já revisado em NaoConformidadeDetalhePage.tsx/PgrDetalhePage.tsx).
  if (!detalhe || !semanal) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={6} />
    );
  }

  const somenteLeitura = semanal.status !== StatusDdsSemanal.EmAndamento;
  const podeEncerrar =
    !somenteLeitura &&
    detalhe.dias.length === 5 &&
    detalhe.dias.every((d) => d.status === StatusDds.Concluido) &&
    (semanal.tipo !== TipoDdsSemanal.Terceirizados || (responsavelTerceirizadaNome.trim() && responsavelTerceirizadaFuncao.trim()));

  return (
    <div>
      <PageHeader
        titulo={`${tipoDdsSemanalLabel[semanal.tipo]} — ${semanal.obraNome}`}
        subtitulo={[
          `Semana: ${semanal.dataInicioSemana?.slice(0, 10)} a ${semanal.dataFimSemana?.slice(0, 10)}`,
          `Responsável/Treinador: ${semanal.responsavelUsuarioNome}`,
          semanal.empresaTerceirizada ? `Empresa terceirizada: ${semanal.empresaTerceirizada}` : null,
          semanal.localFrenteServico ? `Local/Frente de serviço: ${semanal.localFrenteServico}` : null,
        ]
          .filter(Boolean)
          .join(' · ')}
        status={<StatusChip tom={tomStatusSemanal[semanal.status] ?? 'neutro'}>{statusDdsSemanalLabel[semanal.status]}</StatusChip>}
        voltarPara="/prevencao/dds"
        rotuloVoltar="Semanas de DDS"
        acoes={
          <Button icon={<ArrowDownload24Regular />} onClick={baixarPdf} disabled={baixandoPdf}>
            Baixar PDF da semana
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {!somenteLeitura && (
        <div style={{ marginBottom: 16 }}>
          <Card titulo="Encerramento da semana" densidade="compacta">
            {semanal.tipo === TipoDdsSemanal.Terceirizados && (
              <FormSection titulo="Responsável da Empresa Terceirizada" numero={1} primeira>
                <FormGrid>
                  <Campo span={6}>
                    <Field label="Nome do responsável da empresa terceirizada" required>
                      <Input value={responsavelTerceirizadaNome} onChange={(_, d) => setResponsavelTerceirizadaNome(d.value)} />
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Função do responsável da empresa terceirizada" required>
                      <Input value={responsavelTerceirizadaFuncao} onChange={(_, d) => setResponsavelTerceirizadaFuncao(d.value)} />
                    </Field>
                  </Campo>
                </FormGrid>
              </FormSection>
            )}
            <FormRodape info="Só é possível encerrar quando os 5 dias úteis estiverem registrados e concluídos.">
              <Button appearance="primary" icon={<LockClosed24Regular />} onClick={encerrarSemana} disabled={processando || !podeEncerrar}>
                Encerrar semana
              </Button>
            </FormRodape>
          </Card>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 16 }}>
        {detalhe.dias.map((dia, indice) => (
          <Card key={dia.data} densidade="compacta" titulo={NOMES_DIAS[indice]} subtitulo={dia.data?.slice(0, 10)}>
            {dia.ddsId ? (
              <>
                <div style={{ marginBottom: 8 }}>
                  {dia.atividadesNomes.join(', ') || (dia.temaLivreNome ? '' : 'DDS do dia')}
                  {dia.temaLivreNome ? ` + ${dia.temaLivreNome}` : ''}
                </div>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
                  {dia.status !== undefined && dia.status !== null && (
                    <StatusChip tom={tomStatusDia[dia.status] ?? 'neutro'}>{statusDdsLabel[dia.status]}</StatusChip>
                  )}
                  <Legenda>Fotos: {dia.totalFotosEvidencia}/3</Legenda>
                  <Legenda>Participantes: {dia.totalParticipantes}</Legenda>
                </div>
                <Button appearance="primary" icon={<ChevronRight24Regular />} onClick={() => navigate(`/prevencao/dds/dia/${dia.ddsId}`)}>
                  Abrir registro do dia
                </Button>
              </>
            ) : somenteLeitura ? (
              <Legenda>Nenhum registro criado para este dia.</Legenda>
            ) : diaEmCriacao === dia.data ? (
              <FormSection titulo="Registro do dia" primeira>
                <FormGrid>
                  <Campo span={12}>
                    {atividades.length === 0 ? (
                      <Legenda>Nenhuma atividade cadastrada para esta obra.</Legenda>
                    ) : (
                      <Field label="Atividades do dia">
                        <ChipCheckboxGroup
                          aria-label="Atividades do dia"
                          opcoes={atividades.map((a) => ({ id: a.id, rotulo: a.nome }))}
                          selecionados={novoDia.atividadesIds}
                          aoMudar={(ids) => setNovoDia((atual) => ({ ...atual, atividadesIds: ids }))}
                        />
                      </Field>
                    )}
                  </Campo>
                  <Campo span={12}>
                    <Legenda>
                      Cada atividade marcada acima entra automaticamente como um tema do dia (perigo, consequência e
                      controles já cadastrados na Matriz de Riscos dela).
                    </Legenda>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Tema livre (opcional)">
                      <Select
                        value={novoDia.catalogoTemaDdsId}
                        onChange={(_, d) => setNovoDia((atual) => ({ ...atual, catalogoTemaDdsId: d.value }))}
                      >
                        <option value="">Nenhum</option>
                        {catalogoTemas.map((tema) => (
                          <option key={tema.id} value={tema.id}>
                            {tema.nome}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </Campo>
                </FormGrid>
                <FormRodape>
                  <Button appearance="subtle" onClick={() => setDiaEmCriacao(null)}>
                    Cancelar
                  </Button>
                  <Button appearance="primary" icon={<Add24Regular />} onClick={criarRegistroDia} disabled={processando}>
                    Criar registro do dia
                  </Button>
                </FormRodape>
              </FormSection>
            ) : (
              <Button icon={<Add24Regular />} onClick={() => abrirCriacaoDia(dia.data)}>
                Criar registro do dia
              </Button>
            )}
          </Card>
        ))}
      </div>
    </div>
  );
}
