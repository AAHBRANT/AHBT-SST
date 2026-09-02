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
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  resultadoAsoLabel,
  ResultadoAso,
  tipoExameAsoLabel,
  TipoExameAso,
  type Aso,
  type NovoAso,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function asoVazio(trabalhadorId: string): NovoAso {
  return {
    trabalhadorId,
    tipo: TipoExameAso.Admissional,
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

export function AsoTab({ trabalhadorId }: { trabalhadorId: string }) {
  const estilos = usePageStyles();
  const [asos, setAsos] = useState<Aso[]>([]);
  const [novoAso, setNovoAso] = useState<NovoAso>(() => asoVazio(trabalhadorId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setAsos(await api.asos.listar(trabalhadorId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar ASOs.');
    } finally {
      setCarregandoLista(false);
    }
  }

  function vencido(dataValidade: string) {
    return new Date(dataValidade) < new Date(new Date().toDateString());
  }

  useEffect(() => {
    carregar();
    setNovoAso(asoVazio(trabalhadorId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.asos.criar(novoAso);
      setNovoAso(asoVazio(trabalhadorId));
      await carregar();
      sucessoToast('ASO criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar ASO.');
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
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">ASOs do trabalhador</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
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
          Adicionar ASO
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : asos.length === 0 ? (
        <EstadoVazio mensagem="Nenhum ASO cadastrado ainda." />
      ) : (
      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Tipo</TableHeaderCell>
            <TableHeaderCell>Exame</TableHeaderCell>
            <TableHeaderCell>Validade</TableHeaderCell>
            <TableHeaderCell>Resultado</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {asos.map((aso) => (
            <TableRow key={aso.id}>
              <TableCell>{tipoExameAsoLabel[aso.tipo]}</TableCell>
              <TableCell>{aso.dataExame?.slice(0, 10)}</TableCell>
              <TableCell>
                {aso.dataValidade?.slice(0, 10)}
                {vencido(aso.dataValidade) && (
                  <Badge color="danger" appearance="tint" style={{ marginLeft: 8 }}>
                    Vencido
                  </Badge>
                )}
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
                  onClick={() => excluir(aso.id)}
                  aria-label="Excluir"
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      )}
    </div>
  );
}
