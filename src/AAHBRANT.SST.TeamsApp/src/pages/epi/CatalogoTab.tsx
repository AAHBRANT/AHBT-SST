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
import { api, type CatalogoEpi, type NovoCatalogoEpi } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { FotoCatalogoEpi } from './FotoCatalogoEpi';

const epiVazio: NovoCatalogoEpi = {
  nome: '',
  fabricante: '',
  certificadoAprovacaoNumero: '',
  certificadoAprovacaoValidade: '',
  vidaUtilEmMeses: 12,
};

// Catálogo de EPI (item + estoque) do módulo dedicado /epi — antes vivia como aba dentro de
// Pessoas (CatalogoEpiTab), mas o catálogo não é dado de uma pessoa, e sim operacional/compartilhado
// entre entregas; com o módulo próprio de EPI aprovado pelo usuário, a gestão de catálogo/estoque
// passou para cá por inteiro.
export function CatalogoTab() {
  const estilos = usePageStyles();
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [novoEpi, setNovoEpi] = useState<NovoCatalogoEpi>(epiVazio);
  const [fotoNovoEpi, setFotoNovoEpi] = useState<File | null>(null);
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<CatalogoEpi | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setEpis(await api.catalogosEpi.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar catálogo de EPI.');
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
      const { id } = await api.catalogosEpi.criar({
        ...novoEpi,
        certificadoAprovacaoValidade: novoEpi.certificadoAprovacaoValidade || null,
      });
      if (fotoNovoEpi) {
        await api.catalogosEpi.anexarFoto(id, fotoNovoEpi);
      }
      setNovoEpi(epiVazio);
      setFotoNovoEpi(null);
      await carregar();
      sucessoToast('EPI cadastrado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar EPI de catálogo.');
    } finally {
      setCarregando(false);
    }
  }

  async function trocarFoto(epiId: string, arquivo: File) {
    try {
      setErro(null);
      await api.catalogosEpi.anexarFoto(epiId, arquivo);
      await carregar();
      sucessoToast('Foto do EPI atualizada.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a foto do EPI.');
    }
  }

  function iniciarEdicao(epi: CatalogoEpi) {
    setEdicaoId(epi.id);
    setEdicao({ ...epi });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosEpi.atualizar({
        ...edicao,
        certificadoAprovacaoValidade: edicao.certificadoAprovacaoValidade || null,
      });
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('EPI atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar EPI de catálogo.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este item do catálogo de EPI? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogosEpi.excluir(id);
      await carregar();
      sucessoToast('EPI excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir EPI de catálogo.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Catálogo de EPIs</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados do EPI</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col4}>
          <Field label="Nome">
            <Input value={novoEpi.nome} onChange={(_, d) => setNovoEpi({ ...novoEpi, nome: d.value })} />
          </Field>
        </div>
        <div className={estilos.col4}>
          <Field label="Fabricante">
            <Input
              value={novoEpi.fabricante ?? ''}
              onChange={(_, d) => setNovoEpi({ ...novoEpi, fabricante: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col4}>
          <Field label="Nº do CA">
            <Input
              value={novoEpi.certificadoAprovacaoNumero ?? ''}
              onChange={(_, d) => setNovoEpi({ ...novoEpi, certificadoAprovacaoNumero: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col3}>
          <Field label="Validade do CA">
            <CampoData
              value={novoEpi.certificadoAprovacaoValidade ?? ''}
              onChange={(_, d) => setNovoEpi({ ...novoEpi, certificadoAprovacaoValidade: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col3}>
          <Field label="Vida útil (meses)">
            <Input
              type="number"
              value={String(novoEpi.vidaUtilEmMeses)}
              onChange={(_, d) => setNovoEpi({ ...novoEpi, vidaUtilEmMeses: Number(d.value) })}
            />
          </Field>
        </div>
        <div className={estilos.col6}>
          <Field label="Foto do EPI">
            <SeletorFotoCamera
              rotulo={fotoNovoEpi ? fotoNovoEpi.name : 'Tirar foto ou escolher arquivo'}
              tiposAceitos="image/jpeg,image/png"
              aoSelecionarArquivo={(arquivo) => setFotoNovoEpi(arquivo)}
              aoErroValidacao={setErro}
            />
          </Field>
        </div>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar EPI
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : epis.length === 0 ? (
        <EstadoVazio mensagem="Nenhum EPI cadastrado no catálogo ainda." />
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
          {epis.map((epi) =>
            edicaoId === epi.id && edicao ? (
              <TableRow key={epi.id}>
                <TableCell>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <FotoCatalogoEpi catalogoEpiId={epi.id} temFoto={epi.temFoto} tamanho={36} />
                    <SeletorFotoCamera
                      apenasIcone
                      tamanho="small"
                      rotulo="Trocar foto"
                      tiposAceitos="image/jpeg,image/png"
                      aoSelecionarArquivo={(arquivo) => trocarFoto(epi.id, arquivo)}
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
              <TableRow key={epi.id} onClick={() => iniciarEdicao(epi)} style={{ cursor: 'pointer' }}>
                <TableCell>
                  <FotoCatalogoEpi catalogoEpiId={epi.id} temFoto={epi.temFoto} tamanho={36} />
                </TableCell>
                <TableCell>{epi.nome}</TableCell>
                <TableCell>{epi.fabricante}</TableCell>
                <TableCell>{epi.certificadoAprovacaoNumero}</TableCell>
                <TableCell>{epi.certificadoAprovacaoValidade?.slice(0, 10)}</TableCell>
                <TableCell>{epi.vidaUtilEmMeses}</TableCell>
                <TableCell>{epi.saldoTotal}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(e) => {
                      e.stopPropagation();
                      excluir(epi.id);
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
        Clique em uma linha para editar os dados do EPI. O estoque é controlado por Obra na aba
        Estoque.
      </Text>
    </div>
  );
}
