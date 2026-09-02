import { useEffect, useState } from 'react';
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
  Textarea,
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  resultadoAsoLabel,
  ResultadoAso,
  tipoExameAsoLabel,
  type Aso,
  type NovoAso,
  type Trabalhador,
} from '../../lib/api';
import { BadgeVencimento } from '../../components/badges/BadgeVencimento';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function asoVazio(): NovoAso {
  return {
    trabalhadorId: '',
    tipo: 1,
    dataExame: '',
    dataValidade: '',
    resultadoStatus: ResultadoAso.Pendente,
    medicoNome: '',
    medicoCrm: '',
    observacoesClinicas: '',
  };
}

const corResultado: Record<number, 'success' | 'warning' | 'danger' | 'informative'> = {
  1: 'success',
  2: 'warning',
  3: 'danger',
  4: 'informative',
};

// Cross-worker (todos os trabalhadores) — a versão somente-leitura escopada a UM trabalhador já
// existe em PerfilGeralTab.tsx (aba "Geral & ASO" do perfil), que continua inalterada.
export function AsosTab() {
  const estilos = usePageStyles();
  const [asos, setAsos] = useState<Aso[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novoAso, setNovoAso] = useState<NovoAso>(asoVazio());
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<Aso | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaTrabalhadores] = await Promise.all([api.asos.listar(), api.trabalhadores.listar()]);
      setAsos(lista);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar ASOs.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeTrabalhador(id: string) {
    return trabalhadores.find((t) => t.id === id)?.nome ?? id;
  }

  async function criar() {
    if (!novoAso.trabalhadorId || !novoAso.dataExame || !novoAso.dataValidade) {
      setErro('Preencha trabalhador, data do exame e validade.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.asos.criar(novoAso);
      setNovoAso(asoVazio());
      await carregar();
      sucessoToast('ASO registrado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar ASO.');
    } finally {
      setCarregando(false);
    }
  }

  function iniciarEdicao(aso: Aso) {
    setEdicaoId(aso.id);
    setEdicao({ ...aso });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.asos.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('ASO atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar ASO.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este ASO? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.asos.excluir(id);
      await carregar();
      sucessoToast('ASO excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir ASO.');
    }
  }

  return (
    <div>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Novo ASO</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={estilos.form}>
          <Field label="Trabalhador">
            <Select
              value={novoAso.trabalhadorId}
              onChange={(_, d) => setNovoAso({ ...novoAso, trabalhadorId: d.value })}
            >
              <option value="">Selecione</option>
              {trabalhadores.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nome} ({t.matricula})
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Tipo de exame">
            <Select value={novoAso.tipo} onChange={(_, d) => setNovoAso({ ...novoAso, tipo: Number(d.value) })}>
              {Object.entries(tipoExameAsoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Data do exame">
            <CampoData
              value={novoAso.dataExame}
              onChange={(_, d) => setNovoAso({ ...novoAso, dataExame: d.value })}
            />
          </Field>
          <Field label="Validade">
            <CampoData
              value={novoAso.dataValidade}
              onChange={(_, d) => setNovoAso({ ...novoAso, dataValidade: d.value })}
            />
          </Field>
          <Field label="Resultado">
            <Select
              value={novoAso.resultadoStatus}
              onChange={(_, d) => setNovoAso({ ...novoAso, resultadoStatus: Number(d.value) })}
            >
              {Object.entries(resultadoAsoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Médico">
            <Input
              value={novoAso.medicoNome ?? ''}
              onChange={(_, d) => setNovoAso({ ...novoAso, medicoNome: d.value })}
            />
          </Field>
          <Field label="CRM">
            <Input
              value={novoAso.medicoCrm ?? ''}
              onChange={(_, d) => setNovoAso({ ...novoAso, medicoCrm: d.value })}
            />
          </Field>
          <Field label="Observações clínicas">
            <Textarea
              value={novoAso.observacoesClinicas ?? ''}
              onChange={(_, d) => setNovoAso({ ...novoAso, observacoesClinicas: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Registrar ASO
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">ASOs registrados</Text>
        </div>

        {carregandoLista ? (
          <ListaCarregando />
        ) : asos.length === 0 ? (
          <EstadoVazio mensagem="Nenhum ASO cadastrado ainda." />
        ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Trabalhador</TableHeaderCell>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Exame</TableHeaderCell>
              <TableHeaderCell>Validade</TableHeaderCell>
              <TableHeaderCell>Resultado</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {asos.map((aso) =>
              edicaoId === aso.id && edicao ? (
                <TableRow key={aso.id}>
                  <TableCell>{nomeTrabalhador(aso.trabalhadorId)}</TableCell>
                  <TableCell>
                    <Select value={edicao.tipo} onChange={(_, d) => setEdicao({ ...edicao, tipo: Number(d.value) })}>
                      {Object.entries(tipoExameAsoLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </TableCell>
                  <TableCell>
                    <CampoData
                      value={edicao.dataExame?.slice(0, 10)}
                      onChange={(_, d) => setEdicao({ ...edicao, dataExame: d.value })}
                    />
                  </TableCell>
                  <TableCell>
                    <CampoData
                      value={edicao.dataValidade?.slice(0, 10)}
                      onChange={(_, d) => setEdicao({ ...edicao, dataValidade: d.value })}
                    />
                  </TableCell>
                  <TableCell>
                    <Select
                      value={edicao.resultadoStatus}
                      onChange={(_, d) => setEdicao({ ...edicao, resultadoStatus: Number(d.value) })}
                    >
                      {Object.entries(resultadoAsoLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </TableCell>
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
                <TableRow key={aso.id} onClick={() => iniciarEdicao(aso)} style={{ cursor: 'pointer' }}>
                  <TableCell>{nomeTrabalhador(aso.trabalhadorId)}</TableCell>
                  <TableCell>{tipoExameAsoLabel[aso.tipo]}</TableCell>
                  <TableCell>{aso.dataExame?.slice(0, 10)}</TableCell>
                  <TableCell>
                    {aso.dataValidade?.slice(0, 10)}
                    <BadgeVencimento dataValidade={aso.dataValidade} />
                  </TableCell>
                  <TableCell>
                    <Badge color={corResultado[aso.resultadoStatus]} appearance="tint">
                      {resultadoAsoLabel[aso.resultadoStatus]}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Button
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={(e) => {
                        e.stopPropagation();
                        excluir(aso.id);
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
          Clique em uma linha para editar o ASO.
        </Text>
      </div>
    </div>
  );
}
