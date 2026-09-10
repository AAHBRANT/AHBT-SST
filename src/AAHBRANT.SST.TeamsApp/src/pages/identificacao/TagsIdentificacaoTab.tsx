import { useEffect, useState } from 'react';
import { Button, DataTable, Field, FeedbackInline, Input, Select, Text, useConfirmar, type Coluna } from '@ui';
import {
  Add24Regular,
  ArrowDownload24Regular,
  Copy24Regular,
  Delete24Regular,
  Link24Regular,
  LinkDismiss24Regular,
  Open24Regular,
  QrCode24Regular,
  Search24Regular,
} from '@fluentui/react-icons';
import {
  api,
  TipoTag,
  tipoTagLabel,
  StatusTag,
  statusTagLabel,
  TipoEntidadeVinculada,
  type TagIdentificacao,
  type NovaTagIdentificacao,
  type AreaSst,
  type Trabalhador,
  type ResolverTagDto,
  type QrCodeTrabalhador,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { ResolverTagResultado } from './ResolverTagResultado';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const tagVazia: NovaTagIdentificacao = { uid: '', tipo: TipoTag.QrCode };

// Onda 2 Task 9 (camada ui/): lista de Tags de identificação (NFC/QR) — Table→DataTable,
// erro→FeedbackInline, useConfirmarExclusao→useConfirmar (item 1, 4, 6 do Guia). Os dois cards
// (resolver por UID e cadastro/lista) mantêm o layout estilos.card/formGrid de pageStyles (não
// tagueado pro item 2 nesta task).
export function TagsIdentificacaoTab() {
  const estilos = usePageStyles();
  const [tags, setTags] = useState<TagIdentificacao[]>([]);
  const [areas, setAreas] = useState<AreaSst[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaTag, setNovaTag] = useState<NovaTagIdentificacao>(tagVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [gerandoQrFuncionarios, setGerandoQrFuncionarios] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [qrFuncionarios, setQrFuncionarios] = useState<QrCodeTrabalhador[]>([]);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  const [vinculandoId, setVinculandoId] = useState<string | null>(null);
  const [tipoVinculo, setTipoVinculo] = useState<number>(TipoEntidadeVinculada.Area);
  const [entidadeVinculoId, setEntidadeVinculoId] = useState('');

  const [uidBusca, setUidBusca] = useState('');
  const [resultadoBusca, setResultadoBusca] = useState<ResolverTagDto | null>(null);
  const [erroBusca, setErroBusca] = useState<string | null>(null);

  const [tipoVinculoBusca, setTipoVinculoBusca] = useState<number>(TipoEntidadeVinculada.Area);
  const [entidadeVinculoIdBusca, setEntidadeVinculoIdBusca] = useState('');

  async function carregar() {
    try {
      setErro(null);
      const [tgs, ars, trbs] = await Promise.all([
        api.tagsIdentificacao.listar(),
        api.areasSst.listar(),
        api.trabalhadores.listar(),
      ]);
      setTags(tgs);
      setAreas(ars);
      setTrabalhadores(trbs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar tags de identificação.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeEntidadeVinculada(tag: TagIdentificacao) {
    if (!tag.entidadeVinculadaTipo || !tag.entidadeVinculadaId) return '—';
    if (tag.entidadeVinculadaTipo === TipoEntidadeVinculada.Area) {
      return areas.find((a) => a.id === tag.entidadeVinculadaId)?.nome ?? tag.entidadeVinculadaId;
    }
    if (tag.entidadeVinculadaTipo === TipoEntidadeVinculada.Trabalhador) {
      return trabalhadores.find((t) => t.id === tag.entidadeVinculadaId)?.nome ?? tag.entidadeVinculadaId;
    }
    return tag.entidadeVinculadaId;
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.tagsIdentificacao.criar(novaTag);
      setNovaTag(tagVazia);
      await carregar();
      sucessoToast('Tag cadastrada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar tag.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta tag de identificação? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.tagsIdentificacao.excluir(id);
      await carregar();
      sucessoToast('Tag excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir tag.');
    }
  }

  function iniciarVinculo(id: string) {
    setVinculandoId(id);
    setTipoVinculo(TipoEntidadeVinculada.Area);
    setEntidadeVinculoId('');
  }

  async function confirmarVinculo() {
    if (!vinculandoId || !entidadeVinculoId) return;
    try {
      setErro(null);
      await api.tagsIdentificacao.vincular(vinculandoId, tipoVinculo, entidadeVinculoId);
      setVinculandoId(null);
      await carregar();
      sucessoToast('Tag vinculada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao vincular tag.');
    }
  }

  async function desvincular(id: string) {
    try {
      setErro(null);
      await api.tagsIdentificacao.desvincular(id);
      await carregar();
      sucessoToast('Tag desvinculada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao desvincular tag.');
    }
  }

  async function resolverUid() {
    try {
      setErroBusca(null);
      setResultadoBusca(null);
      setEntidadeVinculoIdBusca('');
      const resultado = await api.tagsIdentificacao.resolverPorUid(uidBusca);
      setResultadoBusca(resultado);
    } catch (e) {
      setErroBusca(e instanceof Error ? e.message : 'Falha ao resolver UID.');
    }
  }

  async function vincularPorUid() {
    if (!resultadoBusca || !entidadeVinculoIdBusca) return;
    try {
      setErroBusca(null);
      await api.tagsIdentificacao.vincularPorUid(resultadoBusca.uid, tipoVinculoBusca, entidadeVinculoIdBusca);
      await resolverUid();
      await carregar();
      sucessoToast('Tag vinculada com sucesso.');
    } catch (e) {
      setErroBusca(e instanceof Error ? e.message : 'Falha ao vincular tag.');
    }
  }

  const colunas: Coluna<TagIdentificacao>[] = [
    { chave: 'uid', rotulo: 'UID' },
    { chave: 'tipo', rotulo: 'Tipo', render: (t) => tipoTagLabel[t.tipo] },
    { chave: 'status', rotulo: 'Status', render: (t) => statusTagLabel[t.status] },
    { chave: 'vinculada', rotulo: 'Vinculada a', render: (t) => nomeEntidadeVinculada(t) },
  ];

  const colunasQrFuncionarios: Coluna<QrCodeTrabalhador>[] = [
    { chave: 'trabalhadorNome', rotulo: 'Funcionário' },
    { chave: 'matricula', rotulo: 'Matrícula' },
    { chave: 'obraNome', rotulo: 'Obra' },
    { chave: 'status', rotulo: 'Status', render: (qr) => (qr.jaExistia ? 'Reutilizado' : 'Criado agora') },
  ];

  async function gerarQrFuncionarios() {
    try {
      setGerandoQrFuncionarios(true);
      setErro(null);
      const resultado = await api.tagsIdentificacao.gerarQrCodesTrabalhadores();
      setQrFuncionarios(resultado);
      await carregar();
      sucessoToast(`QR Codes prontos para ${resultado.length} funcionário(s).`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar QR Codes dos funcionários.');
    } finally {
      setGerandoQrFuncionarios(false);
    }
  }

  async function copiarLink(url: string) {
    await navigator.clipboard.writeText(url);
    sucessoToast('Link copiado.');
  }

  async function baixarQrFuncionario(qr: QrCodeTrabalhador) {
    try {
      const blob = await api.tagsIdentificacao.baixarQrCodeTrabalhador(qr.uid);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `qr-${qr.matricula || qr.uid}.png`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar QR Code.');
    }
  }

  return (
    <>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 20 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Resolver tag por UID (leitura de NFC/QR)</Text>
        </div>
        <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Buscar por UID</div>
        <div className={estilos.formGrid}>
          <div className={estilos.col4}>
            <Field label="UID lido">
              <Input value={uidBusca} onChange={(_, d) => setUidBusca(d.value)} />
            </Field>
          </div>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Search24Regular />} onClick={resolverUid} disabled={!uidBusca}>
            Resolver
          </Button>
        </div>
        {erroBusca && (
          <FeedbackInline tom="erro" aoFechar={() => setErroBusca(null)}>
            {erroBusca}
          </FeedbackInline>
        )}
        {resultadoBusca && <ResolverTagResultado resultado={resultadoBusca} />}

        {resultadoBusca && resultadoBusca.status === StatusTag.Disponivel && (
          <>
            <div className={estilos.sectionTitle}>Vincular Tag Encontrada</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col3}>
                <Field label="Vincular a">
                  <Select value={String(tipoVinculoBusca)} onChange={(_, d) => setTipoVinculoBusca(Number(d.value))}>
                    <option value={TipoEntidadeVinculada.Area}>Área</option>
                    <option value={TipoEntidadeVinculada.Trabalhador}>Funcionário</option>
                  </Select>
                </Field>
              </div>
              <div className={estilos.col3}>
                <Field label="Entidade">
                  <Select value={entidadeVinculoIdBusca} onChange={(_, d) => setEntidadeVinculoIdBusca(d.value)}>
                    <option value="">Selecione</option>
                    {tipoVinculoBusca === TipoEntidadeVinculada.Area &&
                      areas.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.nome}
                        </option>
                      ))}
                    {tipoVinculoBusca === TipoEntidadeVinculada.Trabalhador &&
                      trabalhadores.map((t) => (
                        <option key={t.id} value={t.id}>
                          {t.nome}
                        </option>
                      ))}
                  </Select>
                </Field>
              </div>
            </div>
            <div className={estilos.formActions}>
              <Button appearance="primary" icon={<Link24Regular />} onClick={vincularPorUid} disabled={!entidadeVinculoIdBusca}>
                Vincular esta tag
              </Button>
            </div>
          </>
        )}
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Tags de identificação cadastradas</Text>
        </div>

        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}

        <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>QR dos funcionários</div>
        <div className={estilos.formActions}>
          <Button
            appearance="primary"
            icon={<QrCode24Regular />}
            onClick={gerarQrFuncionarios}
            disabled={gerandoQrFuncionarios}
          >
            Gerar ou atualizar QR Codes
          </Button>
        </div>

        {qrFuncionarios.length > 0 && (
          <DataTable
            aria-label="QR Codes dos funcionários"
            colunas={colunasQrFuncionarios}
            linhas={qrFuncionarios}
            chaveLinha={(qr) => qr.tagId}
            acoesLinha={(qr) => (
              <>
                <Button
                  appearance="subtle"
                  icon={<Open24Regular />}
                  onClick={() => window.open(qr.urlPerfil, '_blank')}
                  aria-label="Abrir perfil"
                />
                <Button
                  appearance="subtle"
                  icon={<Copy24Regular />}
                  onClick={() => copiarLink(qr.urlPerfil)}
                  aria-label="Copiar link"
                />
                <Button
                  appearance="subtle"
                  icon={<ArrowDownload24Regular />}
                  onClick={() => baixarQrFuncionario(qr)}
                  aria-label="Baixar QR Code"
                />
              </>
            )}
          />
        )}

        <div className={estilos.sectionTitle}>Nova Tag</div>
        <div className={estilos.formGrid}>
          <div className={estilos.col4}>
            <Field label="UID da tag">
              <Input value={novaTag.uid} onChange={(_, d) => setNovaTag({ ...novaTag, uid: d.value })} />
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Tipo">
              <Select value={String(novaTag.tipo)} onChange={(_, d) => setNovaTag({ ...novaTag, tipo: Number(d.value) })}>
                {Object.entries(tipoTagLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || !novaTag.uid}>
            Cadastrar tag
          </Button>
        </div>

        {vinculandoId && (
          <>
            <div className={estilos.sectionTitle}>Vincular Tag</div>
            <div className={estilos.formGrid}>
              <div className={estilos.col3}>
                <Field label="Vincular a">
                  <Select value={String(tipoVinculo)} onChange={(_, d) => setTipoVinculo(Number(d.value))}>
                    <option value={TipoEntidadeVinculada.Area}>Área</option>
                    <option value={TipoEntidadeVinculada.Trabalhador}>Funcionário</option>
                  </Select>
                </Field>
              </div>
              <div className={estilos.col3}>
                <Field label="Entidade">
                  <Select value={entidadeVinculoId} onChange={(_, d) => setEntidadeVinculoId(d.value)}>
                    <option value="">Selecione</option>
                    {tipoVinculo === TipoEntidadeVinculada.Area &&
                      areas.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.nome}
                        </option>
                      ))}
                    {tipoVinculo === TipoEntidadeVinculada.Trabalhador &&
                      trabalhadores.map((t) => (
                        <option key={t.id} value={t.id}>
                          {t.nome}
                        </option>
                      ))}
                  </Select>
                </Field>
              </div>
            </div>
            <div className={estilos.formActions}>
              <Button appearance="primary" onClick={confirmarVinculo} disabled={!entidadeVinculoId}>
                Confirmar vínculo
              </Button>
              <Button appearance="subtle" onClick={() => setVinculandoId(null)}>
                Cancelar
              </Button>
            </div>
          </>
        )}

        <DataTable
          aria-label="Tags de identificação cadastradas"
          colunas={colunas}
          linhas={tags}
          chaveLinha={(t) => t.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhuma tag de identificação cadastrada ainda.' }}
          acoesLinha={(tag) => (
            <>
              {tag.status === StatusTag.Disponivel && (
                <Button
                  appearance="subtle"
                  icon={<Link24Regular />}
                  onClick={() => iniciarVinculo(tag.id)}
                  aria-label="Vincular"
                />
              )}
              {tag.status === StatusTag.Vinculada && (
                <>
                  <Button
                    appearance="subtle"
                    icon={<QrCode24Regular />}
                    onClick={() => window.open(`${window.location.origin}${window.location.pathname}#/p/${tag.uid}`, '_blank')}
                    aria-label="Abrir crachá/card público desta tag"
                    title="Abrir crachá/card público (o link para gravar na NTAG215 ou gerar o QR Code)"
                  />
                  <Button
                    appearance="subtle"
                    icon={<LinkDismiss24Regular />}
                    onClick={() => desvincular(tag.id)}
                    aria-label="Desvincular"
                  />
                </>
              )}
              <Button
                appearance="subtle"
                icon={<Delete24Regular />}
                onClick={() => excluir(tag.id)}
                aria-label="Excluir"
              />
            </>
          )}
        />
      </div>
    </>
  );
}
