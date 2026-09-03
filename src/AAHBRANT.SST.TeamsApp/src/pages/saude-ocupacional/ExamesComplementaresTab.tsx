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
import { CampoData } from '../../components/CampoData';
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  tipoExameComplementarLabel,
  type Aso,
  type ExameComplementar,
  type NovoExameComplementar,
  type Trabalhador,
} from '../../lib/api';
import { BadgeVencimento } from '../../components/badges/BadgeVencimento';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function exameVazio(): NovoExameComplementar {
  return {
    trabalhadorId: '',
    asoId: '',
    tipo: 1,
    dataRealizacao: '',
    dataValidade: '',
    resultado: '',
    observacoes: '',
    responsavelTecnico: '',
  };
}

export function ExamesComplementaresTab() {
  const estilos = usePageStyles();
  const [exames, setExames] = useState<ExameComplementar[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [novoExame, setNovoExame] = useState<NovoExameComplementar>(exameVazio());
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<ExameComplementar | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaTrabalhadores, listaAsos] = await Promise.all([
        api.examesComplementares.listar(),
        api.trabalhadores.listar(),
        api.asos.listar(),
      ]);
      setExames(lista);
      setTrabalhadores(listaTrabalhadores);
      setAsos(listaAsos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar exames complementares.');
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

  const asosDoTrabalhadorSelecionado = asos.filter((a) => a.trabalhadorId === novoExame.trabalhadorId);

  async function criar() {
    if (!novoExame.trabalhadorId || !novoExame.dataRealizacao || !novoExame.dataValidade || !novoExame.resultado.trim()) {
      setErro('Preencha funcionário, datas e resultado.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.examesComplementares.criar({ ...novoExame, asoId: novoExame.asoId || null });
      setNovoExame(exameVazio());
      await carregar();
      sucessoToast('Exame complementar registrado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar exame complementar.');
    } finally {
      setCarregando(false);
    }
  }

  function iniciarEdicao(exame: ExameComplementar) {
    setEdicaoId(exame.id);
    setEdicao({ ...exame });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.examesComplementares.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('Exame complementar atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar exame complementar.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este exame complementar? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.examesComplementares.excluir(id);
      await carregar();
      sucessoToast('Exame complementar excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir exame complementar.');
    }
  }

  return (
    <div>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Novo exame complementar</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={estilos.form}>
          <Field label="Funcionário">
            <Select
              value={novoExame.trabalhadorId}
              onChange={(_, d) => setNovoExame({ ...novoExame, trabalhadorId: d.value, asoId: '' })}
            >
              <option value="">Selecione</option>
              {trabalhadores.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nome} ({t.matricula})
                </option>
              ))}
            </Select>
          </Field>
          <Field label="ASO vinculado (opcional)">
            <Select
              value={novoExame.asoId ?? ''}
              onChange={(_, d) => setNovoExame({ ...novoExame, asoId: d.value })}
              disabled={!novoExame.trabalhadorId}
            >
              <option value="">Nenhum</option>
              {asosDoTrabalhadorSelecionado.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.dataExame?.slice(0, 10)}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Tipo de exame">
            <Select value={novoExame.tipo} onChange={(_, d) => setNovoExame({ ...novoExame, tipo: Number(d.value) })}>
              {Object.entries(tipoExameComplementarLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Data de realização">
            <CampoData
              value={novoExame.dataRealizacao}
              onChange={(_, d) => setNovoExame({ ...novoExame, dataRealizacao: d.value })}
            />
          </Field>
          <Field label="Validade">
            <CampoData
              value={novoExame.dataValidade}
              onChange={(_, d) => setNovoExame({ ...novoExame, dataValidade: d.value })}
            />
          </Field>
          <Field label="Resultado">
            <Input
              value={novoExame.resultado}
              onChange={(_, d) => setNovoExame({ ...novoExame, resultado: d.value })}
            />
          </Field>
          <Field label="Responsável técnico">
            <Input
              value={novoExame.responsavelTecnico ?? ''}
              onChange={(_, d) => setNovoExame({ ...novoExame, responsavelTecnico: d.value })}
            />
          </Field>
          <Field label="Observações">
            <Input
              value={novoExame.observacoes ?? ''}
              onChange={(_, d) => setNovoExame({ ...novoExame, observacoes: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Registrar exame
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Exames complementares registrados</Text>
        </div>

        {carregandoLista ? (
          <ListaCarregando />
        ) : exames.length === 0 ? (
          <EstadoVazio mensagem="Nenhum exame complementar cadastrado ainda." />
        ) : (
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Funcionário</TableHeaderCell>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Realização</TableHeaderCell>
              <TableHeaderCell>Validade</TableHeaderCell>
              <TableHeaderCell>Resultado</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {exames.map((exame) =>
              edicaoId === exame.id && edicao ? (
                <TableRow key={exame.id}>
                  <TableCell>{nomeTrabalhador(exame.trabalhadorId)}</TableCell>
                  <TableCell>
                    <Select value={edicao.tipo} onChange={(_, d) => setEdicao({ ...edicao, tipo: Number(d.value) })}>
                      {Object.entries(tipoExameComplementarLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </TableCell>
                  <TableCell>
                    <CampoData
                      value={edicao.dataRealizacao?.slice(0, 10)}
                      onChange={(_, d) => setEdicao({ ...edicao, dataRealizacao: d.value })}
                    />
                  </TableCell>
                  <TableCell>
                    <CampoData
                      value={edicao.dataValidade?.slice(0, 10)}
                      onChange={(_, d) => setEdicao({ ...edicao, dataValidade: d.value })}
                    />
                  </TableCell>
                  <TableCell>
                    <Input
                      value={edicao.resultado}
                      onChange={(_, d) => setEdicao({ ...edicao, resultado: d.value })}
                    />
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
                <TableRow key={exame.id} onClick={() => iniciarEdicao(exame)} style={{ cursor: 'pointer' }}>
                  <TableCell>{nomeTrabalhador(exame.trabalhadorId)}</TableCell>
                  <TableCell>{tipoExameComplementarLabel[exame.tipo]}</TableCell>
                  <TableCell>{exame.dataRealizacao?.slice(0, 10)}</TableCell>
                  <TableCell>
                    {exame.dataValidade?.slice(0, 10)}
                    <BadgeVencimento dataValidade={exame.dataValidade} />
                  </TableCell>
                  <TableCell>{exame.resultado}</TableCell>
                  <TableCell>
                    <Button
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={(e) => {
                        e.stopPropagation();
                        excluir(exame.id);
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
          Clique em uma linha para editar o exame complementar.
        </Text>
      </div>
    </div>
  );
}
