import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular, Link24Regular, LinkDismiss24Regular, Search24Regular } from '@fluentui/react-icons';
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
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { ResolverTagResultado } from './ResolverTagResultado';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const tagVazia: NovaTagIdentificacao = { uid: '', tipo: TipoTag.QrCode };

export function TagsIdentificacaoTab() {
  const estilos = usePageStyles();
  const [tags, setTags] = useState<TagIdentificacao[]>([]);
  const [areas, setAreas] = useState<AreaSst[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaTag, setNovaTag] = useState<NovaTagIdentificacao>(tagVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
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

  return (
    <>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 20 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Resolver tag por UID (leitura de NFC/QR)</Text>
        </div>
        <div className={estilos.form}>
          <Field label="UID lido">
            <Input value={uidBusca} onChange={(_, d) => setUidBusca(d.value)} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Search24Regular />} onClick={resolverUid} disabled={!uidBusca}>
            Resolver
          </Button>
        </div>
        {erroBusca && <Text className={estilos.erro}>{erroBusca}</Text>}
        {resultadoBusca && <ResolverTagResultado resultado={resultadoBusca} />}

        {resultadoBusca && resultadoBusca.status === StatusTag.Disponivel && (
          <div className={estilos.form} style={{ marginTop: 12 }}>
            <Field label="Vincular a">
              <Select value={String(tipoVinculoBusca)} onChange={(_, d) => setTipoVinculoBusca(Number(d.value))}>
                <option value={TipoEntidadeVinculada.Area}>Área</option>
                <option value={TipoEntidadeVinculada.Trabalhador}>Funcionário</option>
              </Select>
            </Field>
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
            <div className={estilos.formActions}>
              <Button appearance="primary" icon={<Link24Regular />} onClick={vincularPorUid} disabled={!entidadeVinculoIdBusca}>
                Vincular esta tag
              </Button>
            </div>
          </div>
        )}
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Tags de identificação cadastradas</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={estilos.form}>
          <Field label="UID da tag">
            <Input value={novaTag.uid} onChange={(_, d) => setNovaTag({ ...novaTag, uid: d.value })} />
          </Field>
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
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || !novaTag.uid}>
            Cadastrar tag
          </Button>
        </div>

        {vinculandoId && (
          <div className={estilos.form} style={{ marginTop: 12 }}>
            <Field label="Vincular a">
              <Select value={String(tipoVinculo)} onChange={(_, d) => setTipoVinculo(Number(d.value))}>
                <option value={TipoEntidadeVinculada.Area}>Área</option>
                <option value={TipoEntidadeVinculada.Trabalhador}>Funcionário</option>
              </Select>
            </Field>
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
            <div className={estilos.formActions}>
              <Button appearance="primary" onClick={confirmarVinculo} disabled={!entidadeVinculoId}>
                Confirmar vínculo
              </Button>
              <Button appearance="subtle" onClick={() => setVinculandoId(null)}>
                Cancelar
              </Button>
            </div>
          </div>
        )}

        {carregandoLista ? (
          <ListaCarregando />
        ) : tags.length === 0 ? (
          <EstadoVazio mensagem="Nenhuma tag de identificação cadastrada ainda." />
        ) : (
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>UID</TableHeaderCell>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Vinculada a</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {tags.map((tag) => (
              <TableRow key={tag.id}>
                <TableCell>{tag.uid}</TableCell>
                <TableCell>{tipoTagLabel[tag.tipo]}</TableCell>
                <TableCell>{statusTagLabel[tag.status]}</TableCell>
                <TableCell>{nomeEntidadeVinculada(tag)}</TableCell>
                <TableCell>
                  {tag.status === StatusTag.Disponivel && (
                    <Button
                      appearance="subtle"
                      icon={<Link24Regular />}
                      onClick={() => iniciarVinculo(tag.id)}
                      aria-label="Vincular"
                    />
                  )}
                  {tag.status === StatusTag.Vinculada && (
                    <Button
                      appearance="subtle"
                      icon={<LinkDismiss24Regular />}
                      onClick={() => desvincular(tag.id)}
                      aria-label="Desvincular"
                    />
                  )}
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={() => excluir(tag.id)}
                    aria-label="Excluir"
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        )}
      </div>
    </>
  );
}
