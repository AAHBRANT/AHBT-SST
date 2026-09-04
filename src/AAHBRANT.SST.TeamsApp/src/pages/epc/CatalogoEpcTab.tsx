import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import { api, type CatalogoEpc, type NovoCatalogoEpc } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { FotoCatalogoEpc } from './FotoCatalogoEpc';

const epcVazio: NovoCatalogoEpc = {
  nome: '',
  fabricante: '',
  certificadoAprovacaoNumero: '',
  certificadoAprovacaoValidade: '',
  vidaUtilEmMeses: 12,
};

// Catálogo de EPC — mesma estrutura do Catálogo de EPI (pedido do usuário, 04/09: aba própria e
// separada de EPI). Sem CódigoBarras (isso ficou só na Entrega Rápida de EPI, não usado aqui).
export function CatalogoEpcTab() {
  const estilos = usePageStyles();
  const [epcs, setEpcs] = useState<CatalogoEpc[]>([]);
  const [novoEpc, setNovoEpc] = useState<NovoCatalogoEpc>(epcVazio);
  const [fotoNovoEpc, setFotoNovoEpc] = useState<File | null>(null);
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<CatalogoEpc | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setEpcs(await api.catalogosEpc.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar catálogo de EPC.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      const { id } = await api.catalogosEpc.criar({
        ...novoEpc,
        certificadoAprovacaoValidade: novoEpc.certificadoAprovacaoValidade || null,
      });
      if (fotoNovoEpc) {
        await api.catalogosEpc.anexarFoto(id, fotoNovoEpc);
      }
      setNovoEpc(epcVazio);
      setFotoNovoEpc(null);
      await carregar();
      sucessoToast('EPC cadastrado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar EPC de catálogo.');
    } finally {
      setCarregando(false);
    }
  }

  async function trocarFoto(epcId: string, arquivo: File) {
    try {
      setErro(null);
      await api.catalogosEpc.anexarFoto(epcId, arquivo);
      await carregar();
      sucessoToast('Foto do EPC atualizada.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a foto do EPC.');
    }
  }

  function iniciarEdicao(epc: CatalogoEpc) {
    setEdicaoId(epc.id);
    setEdicao({ ...epc });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosEpc.atualizar({
        ...edicao,
        certificadoAprovacaoValidade: edicao.certificadoAprovacaoValidade || null,
      });
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('EPC atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar EPC de catálogo.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este item do catálogo de EPC? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogosEpc.excluir(id);
      await carregar();
      sucessoToast('EPC excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir EPC de catálogo.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Catálogo de EPCs</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados do EPC</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col4}>
          <Field label="Nome">
            <Input value={novoEpc.nome} onChange={(_, d) => setNovoEpc({ ...novoEpc, nome: d.value })} />
          </Field>
        </div>
        <div className={estilos.col4}>
          <Field label="Fabricante">
            <Input
              value={novoEpc.fabricante ?? ''}
              onChange={(_, d) => setNovoEpc({ ...novoEpc, fabricante: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col4}>
          <Field label="Nº do CA (se houver)">
            <Input
              value={novoEpc.certificadoAprovacaoNumero ?? ''}
              onChange={(_, d) => setNovoEpc({ ...novoEpc, certificadoAprovacaoNumero: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col3}>
          <Field label="Validade do CA">
            <CampoData
              value={novoEpc.certificadoAprovacaoValidade ?? ''}
              onChange={(_, d) => setNovoEpc({ ...novoEpc, certificadoAprovacaoValidade: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col3}>
          <Field label="Vida útil (meses)">
            <Input
              type="number"
              value={String(novoEpc.vidaUtilEmMeses)}
              onChange={(_, d) => setNovoEpc({ ...novoEpc, vidaUtilEmMeses: Number(d.value) })}
            />
          </Field>
        </div>
        <div className={estilos.col6}>
          <Field label="Foto do EPC">
            <SeletorFotoCamera
              rotulo={fotoNovoEpc ? fotoNovoEpc.name : 'Tirar foto ou escolher arquivo'}
              tiposAceitos="image/jpeg,image/png"
              aoSelecionarArquivo={(arquivo) => setFotoNovoEpc(arquivo)}
              aoErroValidacao={setErro}
            />
          </Field>
        </div>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar EPC
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : epcs.length === 0 ? (
        <EstadoVazio mensagem="Nenhum EPC cadastrado no catálogo ainda." />
      ) : (
      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Foto</TableHeaderCell>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Fabricante</TableHeaderCell>
            <TableHeaderCell>Nº do CA</TableHeaderCell>
            <TableHeaderCell>Validade do CA</TableHeaderCell>
            <TableHeaderCell>Vida útil (meses)</TableHeaderCell>
            <TableHeaderCell>Estoque total</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {epcs.map((epc) =>
            edicaoId === epc.id && edicao ? (
              <TableRow key={epc.id}>
                <TableCell>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <FotoCatalogoEpc catalogoEpcId={epc.id} temFoto={epc.temFoto} tamanho={36} />
                    <SeletorFotoCamera
                      apenasIcone
                      tamanho="small"
                      rotulo="Trocar foto"
                      tiposAceitos="image/jpeg,image/png"
                      aoSelecionarArquivo={(arquivo) => trocarFoto(epc.id, arquivo)}
                      aoErroValidacao={setErro}
                    />
                  </div>
                </TableCell>
                <TableCell>
                  <Input value={edicao.nome} onChange={(_, d) => setEdicao({ ...edicao, nome: d.value })} />
                </TableCell>
                <TableCell>
                  <Input
                    value={edicao.fabricante ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, fabricante: d.value })}
                  />
                </TableCell>
                <TableCell>
                  <Input
                    value={edicao.certificadoAprovacaoNumero ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, certificadoAprovacaoNumero: d.value })}
                  />
                </TableCell>
                <TableCell>
                  <CampoData
                    value={edicao.certificadoAprovacaoValidade?.slice(0, 10) ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, certificadoAprovacaoValidade: d.value })}
                  />
                </TableCell>
                <TableCell>
                  <Input
                    type="number"
                    value={String(edicao.vidaUtilEmMeses)}
                    onChange={(_, d) => setEdicao({ ...edicao, vidaUtilEmMeses: Number(d.value) })}
                  />
                </TableCell>
                <TableCell>{edicao.saldoTotal}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Save24Regular />}
                    onClick={salvarEdicao}
                    disabled={carregando}
                    aria-label="Salvar"
                  />
                </TableCell>
              </TableRow>
            ) : (
              <TableRow key={epc.id} onClick={() => iniciarEdicao(epc)} style={{ cursor: 'pointer' }}>
                <TableCell>
                  <FotoCatalogoEpc catalogoEpcId={epc.id} temFoto={epc.temFoto} tamanho={36} />
                </TableCell>
                <TableCell>{epc.nome}</TableCell>
                <TableCell>{epc.fabricante}</TableCell>
                <TableCell>{epc.certificadoAprovacaoNumero}</TableCell>
                <TableCell>{epc.certificadoAprovacaoValidade?.slice(0, 10)}</TableCell>
                <TableCell>{epc.vidaUtilEmMeses}</TableCell>
                <TableCell>{epc.saldoTotal}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(e) => {
                      e.stopPropagation();
                      excluir(epc.id);
                    }}
                    aria-label="Excluir"
                  />
                </TableCell>
              </TableRow>
            ),
          )}
        </TableBody>
      </Table>
      )}
      <Text size={200} style={{ display: 'block', marginTop: 8 }}>
        Clique em uma linha para editar os dados do EPC. O estoque é controlado por Obra na aba
        Estoque.
      </Text>
    </div>
  );
}
