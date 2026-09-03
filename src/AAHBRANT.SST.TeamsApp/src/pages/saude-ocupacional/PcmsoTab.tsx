import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
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
import { CampoData } from '../../components/CampoData';
import { Add24Regular, ChevronRight24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  statusPcmsoDocumentoLabel,
  StatusPcmsoDocumento,
  type NovoPcmso,
  type Obra,
  type Pcmso,
} from '../../lib/api';
import { BadgeVencimento } from '../../components/badges/BadgeVencimento';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function pcmsoVazio(): NovoPcmso {
  return {
    nome: '',
    versao: '',
    validade: '',
    dataEmissao: '',
    responsavelUsuarioId: '',
    obraId: '',
    setorId: '',
    arquivo: '',
    medicoResponsavelNome: '',
    medicoResponsavelCrm: '',
    funcoesContempladas: '',
    riscosConsiderados: '',
    examesPrevistos: '',
    periodicidades: '',
    unidadesObrasAbrangidas: '',
  };
}

// Edição completa dos campos clínicos e o Plano de Ação vinculado ficam em PcmsoDetalhePage.tsx
// (mesmo padrão de navegação lista→detalhe usado por PgrsTab.tsx e NaoConformidadesTab.tsx).
export function PcmsoTab() {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [pcmsos, setPcmsos] = useState<Pcmso[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novoPcmso, setNovoPcmso] = useState<NovoPcmso>(pcmsoVazio());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaObras] = await Promise.all([api.pcmsos.listar(), api.obras.listar()]);
      setPcmsos(lista);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar PCMSOs.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id?: string | null) {
    if (!id) return '—';
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  async function criar() {
    if (!novoPcmso.nome.trim() || !novoPcmso.dataEmissao) {
      setErro('Preencha nome e data de emissão.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.pcmsos.criar({
        ...novoPcmso,
        versao: novoPcmso.versao || null,
        validade: novoPcmso.validade || null,
        responsavelUsuarioId: novoPcmso.responsavelUsuarioId || null,
        obraId: novoPcmso.obraId || null,
        setorId: novoPcmso.setorId || null,
      });
      setNovoPcmso(pcmsoVazio());
      await carregar();
      sucessoToast('PCMSO criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar PCMSO.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string, evento: React.MouseEvent) {
    evento.stopPropagation();
    if (!(await confirmar('Excluir este PCMSO? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.pcmsos.excluir(id);
      await carregar();
      sucessoToast('PCMSO excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir PCMSO.');
    }
  }

  return (
    <div>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Novo PCMSO</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={estilos.formGrid}>
          <div className={estilos.col4}>
            <Field label="Nome do Documento" required>
              <Input value={novoPcmso.nome} onChange={(_, d) => setNovoPcmso({ ...novoPcmso, nome: d.value })} />
            </Field>
          </div>
          <div className={estilos.col2}>
            <Field label="Versão">
              <Input
                value={novoPcmso.versao ?? ''}
                onChange={(_, d) => setNovoPcmso({ ...novoPcmso, versao: d.value })}
              />
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Obra">
              <Select
                value={novoPcmso.obraId ?? ''}
                onChange={(_, d) => setNovoPcmso({ ...novoPcmso, obraId: d.value, setorId: '' })}
              >
                <option value="">Nenhuma</option>
                {obras.map((obra) => (
                  <option key={obra.id} value={obra.id}>
                    {obra.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Data de emissão" required>
              <CampoData
                value={novoPcmso.dataEmissao}
                onChange={(_, d) => setNovoPcmso({ ...novoPcmso, dataEmissao: d.value })}
              />
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Validade">
              <CampoData
                value={novoPcmso.validade ?? ''}
                onChange={(_, d) => setNovoPcmso({ ...novoPcmso, validade: d.value })}
              />
            </Field>
          </div>
          <div className={estilos.col5}>
            <Field label="Médico responsável">
              <Input
                value={novoPcmso.medicoResponsavelNome ?? ''}
                onChange={(_, d) => setNovoPcmso({ ...novoPcmso, medicoResponsavelNome: d.value })}
              />
            </Field>
          </div>
        </div>
        <div className={estilos.footer}>
          <Text className={estilos.footerInfo}>
            Os demais campos (CRM, funções/riscos/exames contemplados, periodicidades, unidades
            abrangidas, status e Plano de Ação) são preenchidos na tela de detalhe, após criar o registro.
          </Text>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Adicionar PCMSO
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">PCMSOs cadastrados</Text>
        </div>

        {carregandoLista ? (
          <ListaCarregando />
        ) : pcmsos.length === 0 ? (
          <EstadoVazio mensagem="Nenhum PCMSO cadastrado ainda." />
        ) : (
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Emissão</TableHeaderCell>
              <TableHeaderCell>Validade</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {pcmsos.map((pcmso) => (
              <TableRow
                key={pcmso.id}
                onClick={() => navigate(`/saude-ocupacional/pcmso/${pcmso.id}`)}
                style={{ cursor: 'pointer' }}
              >
                <TableCell>{pcmso.nome}</TableCell>
                <TableCell>{nomeObra(pcmso.obraId)}</TableCell>
                <TableCell>{pcmso.dataEmissao?.slice(0, 10)}</TableCell>
                <TableCell>
                  {pcmso.validade?.slice(0, 10) ?? '—'}
                  <BadgeVencimento dataValidade={pcmso.validade} />
                </TableCell>
                <TableCell>
                  <Badge
                    appearance="tint"
                    color={pcmso.status === StatusPcmsoDocumento.Vigente ? 'success' : 'informative'}
                  >
                    {statusPcmsoDocumentoLabel[pcmso.status]}
                  </Badge>
                </TableCell>
                <TableCell>
                  <div style={{ display: 'flex', gap: 4 }}>
                    <Button
                      appearance="subtle"
                      icon={<ChevronRight24Regular />}
                      onClick={() => navigate(`/saude-ocupacional/pcmso/${pcmso.id}`)}
                      aria-label="Ver PCMSO"
                    />
                    <Button
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={(evento) => excluir(pcmso.id, evento)}
                      aria-label="Excluir"
                    />
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        )}
      </div>
    </div>
  );
}
