import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Checkbox,
  Field,
  Input,
  Select,
  Text,
} from '@fluentui/react-components';
import {
  Add24Regular,
  ArrowDownload24Regular,
  ArrowLeft24Regular,
  ChevronRight24Regular,
  LockClosed24Regular,
} from '@fluentui/react-icons';
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
import { usePageStyles, useCheckboxChipStyles } from '../pageStyles';

const NOMES_DIAS = ['Segunda-feira', 'Terça-feira', 'Quarta-feira', 'Quinta-feira', 'Sexta-feira'];

function novoDiaVazio() {
  return { atividadesIds: [] as string[], catalogoTemaDdsId: '' };
}

// Semana (contêiner) do DDS reformulado (31/08) — cada um dos 5 dias úteis é um registro diário
// próprio (DdsDetalhePage), feito e assinado no seu próprio dia. Esta tela mostra os 5 slots
// Seg-Sex: dia já registrado abre o detalhe; dia em aberto mostra o formulário de criação do
// registro daquele dia (seleção de atividades + origem do tema, ver CriarDdsCommand).
export function DdsSemanalDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const estilosChip = useCheckboxChipStyles();
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

  function alternarAtividade(atividadeId: string, marcado: boolean) {
    setNovoDia((atual) => ({
      ...atual,
      atividadesIds: marcado ? [...atual.atividadesIds, atividadeId] : atual.atividadesIds.filter((a) => a !== atividadeId),
    }));
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

  if (!id) {
    return <Text>Semana de DDS não encontrada.</Text>;
  }

  const semanal = detalhe?.semanal;
  const somenteLeitura = semanal?.status !== StatusDdsSemanal.EmAndamento;
  const atividadesDaObra = atividades;
  const podeEncerrar =
    !!semanal &&
    !somenteLeitura &&
    (detalhe?.dias.length ?? 0) === 5 &&
    detalhe!.dias.every((d) => d.status === StatusDds.Concluido) &&
    (semanal.tipo !== TipoDdsSemanal.Terceirizados || (responsavelTerceirizadaNome.trim() && responsavelTerceirizadaFuncao.trim()));

  return (
    <div>
      <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={() => navigate('/prevencao/dds')} style={{ marginBottom: 12 }}>
        Voltar para as semanas de DDS
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {semanal ? (
          <>
            <Text size={500} weight="semibold">
              {tipoDdsSemanalLabel[semanal.tipo]} — {semanal.obraNome}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>
                Semana: {semanal.dataInicioSemana?.slice(0, 10)} a {semanal.dataFimSemana?.slice(0, 10)}
              </Text>
              <Text>Responsável/Treinador: {semanal.responsavelUsuarioNome}</Text>
              <Badge appearance="tint">{statusDdsSemanalLabel[semanal.status]}</Badge>
            </div>
            {semanal.empresaTerceirizada && <Text style={{ display: 'block', marginTop: 4 }}>Empresa terceirizada: {semanal.empresaTerceirizada}</Text>}
            {semanal.localFrenteServico && <Text style={{ display: 'block', marginTop: 4 }}>Local/Frente de serviço: {semanal.localFrenteServico}</Text>}

            <div className={estilos.formActions} style={{ marginTop: 16 }}>
              <Button icon={<ArrowDownload24Regular />} onClick={baixarPdf} disabled={baixandoPdf}>
                Baixar PDF da semana
              </Button>
            </div>

            {!somenteLeitura && (
              <div style={{ marginTop: 16, borderTop: '1px solid #eee', paddingTop: 16 }}>
                <Text weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
                  Encerramento da semana
                </Text>
                {semanal.tipo === TipoDdsSemanal.Terceirizados && (
                  <>
                    <div className={estilos.sectionTitle}>Responsável da Empresa Terceirizada</div>
                    <div className={estilos.formGrid}>
                      <div className={estilos.col6}>
                        <Field label="Nome do responsável da empresa terceirizada">
                          <Input value={responsavelTerceirizadaNome} onChange={(_, d) => setResponsavelTerceirizadaNome(d.value)} />
                        </Field>
                      </div>
                      <div className={estilos.col6}>
                        <Field label="Função do responsável da empresa terceirizada">
                          <Input value={responsavelTerceirizadaFuncao} onChange={(_, d) => setResponsavelTerceirizadaFuncao(d.value)} />
                        </Field>
                      </div>
                    </div>
                  </>
                )}
                <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
                  Só é possível encerrar quando os 5 dias úteis estiverem registrados e concluídos.
                </Text>
                <Button appearance="primary" icon={<LockClosed24Regular />} onClick={encerrarSemana} disabled={processando || !podeEncerrar}>
                  Encerrar semana
                </Button>
              </div>
            )}
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 16 }}>
        {detalhe?.dias.map((dia, indice) => (
          <div key={dia.data} className={estilos.card}>
            <Text weight="semibold" style={{ display: 'block', marginBottom: 4 }}>
              {NOMES_DIAS[indice]}
            </Text>
            <Text size={200} style={{ display: 'block', marginBottom: 8 }}>
              {dia.data?.slice(0, 10)}
            </Text>

            {dia.ddsId ? (
              <>
                <Text style={{ display: 'block', marginBottom: 4 }}>
                  {dia.atividadesNomes.join(', ') || (dia.temaLivreNome ? '' : 'DDS do dia')}
                  {dia.temaLivreNome ? ` + ${dia.temaLivreNome}` : ''}
                </Text>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 8, flexWrap: 'wrap' }}>
                  {dia.status !== undefined && dia.status !== null && (
                    <Badge appearance="tint">{statusDdsLabel[dia.status]}</Badge>
                  )}
                  <Text size={200}>Fotos: {dia.totalFotosEvidencia}/3</Text>
                  <Text size={200}>Participantes: {dia.totalParticipantes}</Text>
                </div>
                <Button
                  appearance="primary"
                  icon={<ChevronRight24Regular />}
                  onClick={() => navigate(`/prevencao/dds/dia/${dia.ddsId}`)}
                >
                  Abrir registro do dia
                </Button>
              </>
            ) : somenteLeitura ? (
              <Text size={200}>Nenhum registro criado para este dia.</Text>
            ) : diaEmCriacao === dia.data ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                <Field label="Atividades do dia">
                  {atividadesDaObra.length === 0 ? (
                    <Text size={200}>Nenhuma atividade cadastrada para esta obra.</Text>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                      {atividadesDaObra.map((atividade) => (
                        <Checkbox
                          key={atividade.id}
                          className={estilosChip.chip}
                          label={atividade.nome}
                          checked={novoDia.atividadesIds.includes(atividade.id)}
                          onChange={(_, d) => alternarAtividade(atividade.id, !!d.checked)}
                        />
                      ))}
                    </div>
                  )}
                </Field>

                <Text size={200} style={{ display: 'block' }}>
                  Cada atividade marcada acima entra automaticamente como um tema do dia
                  (perigo, consequência e controles já cadastrados na Matriz de Riscos dela).
                </Text>

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

                <div className={estilos.formActions}>
                  <Button appearance="subtle" onClick={() => setDiaEmCriacao(null)}>
                    Cancelar
                  </Button>
                  <Button appearance="primary" icon={<Add24Regular />} onClick={criarRegistroDia} disabled={processando}>
                    Criar registro do dia
                  </Button>
                </div>
              </div>
            ) : (
              <Button icon={<Add24Regular />} onClick={() => abrirCriacaoDia(dia.data)}>
                Criar registro do dia
              </Button>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
