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
  type Aptidao,
  type NovaAptidao,
  type Trabalhador,
} from '../../lib/api';
import { BadgeVencimento } from '../../components/badges/BadgeVencimento';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function aptidaoVazia(): NovaAptidao {
  return {
    trabalhadorId: '',
    atividadeCritica: '',
    aptidao: ResultadoAso.Pendente,
    dataAvaliacao: '',
    dataValidade: '',
    medicoResponsavel: '',
    observacoes: '',
  };
}

const corResultado: Record<number, 'success' | 'warning' | 'danger' | 'informative'> = {
  1: 'success',
  2: 'warning',
  3: 'danger',
  4: 'informative',
};

// Aptidão para atividade crítica (ex.: trabalho em altura, espaço confinado) — distinta do ASO
// geral, embora reaproveite o mesmo enum de resultado (Apto/Apto com restrição/Inapto/Pendente).
export function AptidoesTab() {
  const estilos = usePageStyles();
  const [aptidoes, setAptidoes] = useState<Aptidao[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaAptidao, setNovaAptidao] = useState<NovaAptidao>(aptidaoVazia());
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<Aptidao | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaTrabalhadores] = await Promise.all([api.aptidoes.listar(), api.trabalhadores.listar()]);
      setAptidoes(lista);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar aptidões.');
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
    if (!novaAptidao.trabalhadorId || !novaAptidao.atividadeCritica.trim() || !novaAptidao.dataAvaliacao) {
      setErro('Preencha trabalhador, atividade crítica e data da avaliação.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.aptidoes.criar({ ...novaAptidao, dataValidade: novaAptidao.dataValidade || null });
      setNovaAptidao(aptidaoVazia());
      await carregar();
      sucessoToast('Aptidão registrada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar aptidão.');
    } finally {
      setCarregando(false);
    }
  }

  function iniciarEdicao(aptidao: Aptidao) {
    setEdicaoId(aptidao.id);
    setEdicao({ ...aptidao });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.aptidoes.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('Aptidão atualizada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar aptidão.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta aptidão? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.aptidoes.excluir(id);
      await carregar();
      sucessoToast('Aptidão excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir aptidão.');
    }
  }

  return (
    <div>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Nova aptidão para atividade crítica</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={estilos.form}>
          <Field label="Trabalhador">
            <Select
              value={novaAptidao.trabalhadorId}
              onChange={(_, d) => setNovaAptidao({ ...novaAptidao, trabalhadorId: d.value })}
            >
              <option value="">Selecione</option>
              {trabalhadores.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.nome} ({t.matricula})
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Atividade crítica">
            <Input
              placeholder="Ex.: Trabalho em altura, Espaço confinado"
              value={novaAptidao.atividadeCritica}
              onChange={(_, d) => setNovaAptidao({ ...novaAptidao, atividadeCritica: d.value })}
            />
          </Field>
          <Field label="Data da avaliação">
            <CampoData
              value={novaAptidao.dataAvaliacao}
              onChange={(_, d) => setNovaAptidao({ ...novaAptidao, dataAvaliacao: d.value })}
            />
          </Field>
          <Field label="Validade (opcional)">
            <CampoData
              value={novaAptidao.dataValidade ?? ''}
              onChange={(_, d) => setNovaAptidao({ ...novaAptidao, dataValidade: d.value })}
            />
          </Field>
          <Field label="Resultado">
            <Select
              value={novaAptidao.aptidao}
              onChange={(_, d) => setNovaAptidao({ ...novaAptidao, aptidao: Number(d.value) })}
            >
              {Object.entries(resultadoAsoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Médico responsável">
            <Input
              value={novaAptidao.medicoResponsavel ?? ''}
              onChange={(_, d) => setNovaAptidao({ ...novaAptidao, medicoResponsavel: d.value })}
            />
          </Field>
          <Field label="Observações">
            <Textarea
              value={novaAptidao.observacoes ?? ''}
              onChange={(_, d) => setNovaAptidao({ ...novaAptidao, observacoes: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Registrar aptidão
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Aptidões registradas</Text>
        </div>

        {carregandoLista ? (
          <ListaCarregando />
        ) : aptidoes.length === 0 ? (
          <EstadoVazio mensagem="Nenhuma aptidão cadastrada ainda." />
        ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Trabalhador</TableHeaderCell>
              <TableHeaderCell>Atividade crítica</TableHeaderCell>
              <TableHeaderCell>Avaliação</TableHeaderCell>
              <TableHeaderCell>Validade</TableHeaderCell>
              <TableHeaderCell>Resultado</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {aptidoes.map((aptidao) =>
              edicaoId === aptidao.id && edicao ? (
                <TableRow key={aptidao.id}>
                  <TableCell>{nomeTrabalhador(aptidao.trabalhadorId)}</TableCell>
                  <TableCell>
                    <Input
                      value={edicao.atividadeCritica}
                      onChange={(_, d) => setEdicao({ ...edicao, atividadeCritica: d.value })}
                    />
                  </TableCell>
                  <TableCell>
                    <CampoData
                      value={edicao.dataAvaliacao?.slice(0, 10)}
                      onChange={(_, d) => setEdicao({ ...edicao, dataAvaliacao: d.value })}
                    />
                  </TableCell>
                  <TableCell>
                    <CampoData
                      value={edicao.dataValidade?.slice(0, 10) ?? ''}
                      onChange={(_, d) => setEdicao({ ...edicao, dataValidade: d.value })}
                    />
                  </TableCell>
                  <TableCell>
                    <Select
                      value={edicao.aptidao}
                      onChange={(_, d) => setEdicao({ ...edicao, aptidao: Number(d.value) })}
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
                <TableRow key={aptidao.id} onClick={() => iniciarEdicao(aptidao)} style={{ cursor: 'pointer' }}>
                  <TableCell>{nomeTrabalhador(aptidao.trabalhadorId)}</TableCell>
                  <TableCell>{aptidao.atividadeCritica}</TableCell>
                  <TableCell>{aptidao.dataAvaliacao?.slice(0, 10)}</TableCell>
                  <TableCell>
                    {aptidao.dataValidade?.slice(0, 10)}
                    <BadgeVencimento dataValidade={aptidao.dataValidade} />
                  </TableCell>
                  <TableCell>
                    <Badge color={corResultado[aptidao.aptidao]} appearance="tint">
                      {resultadoAsoLabel[aptidao.aptidao]}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Button
                      appearance="subtle"
                      icon={<Delete24Regular />}
                      onClick={(e) => {
                        e.stopPropagation();
                        excluir(aptidao.id);
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
          Clique em uma linha para editar a aptidão.
        </Text>
      </div>
    </div>
  );
}
