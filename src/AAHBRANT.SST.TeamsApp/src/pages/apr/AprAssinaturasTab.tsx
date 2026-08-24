import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { Add24Regular } from '@fluentui/react-icons';
import {
  api,
  papelAssinaturaAprLabel,
  PapelAssinaturaApr,
  type AprAssinatura,
  type NovaAprAssinatura,
  type Trabalhador,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function assinaturaVazia(aprId: string): NovaAprAssinatura {
  return { aprId, trabalhadorId: '', papel: PapelAssinaturaApr.Executante };
}

// Assinatura (§17) é registro de ciência append-only — sem edição/exclusão, mesmo padrão de
// PgrRevisoesTab. Não é assinatura criptográfica/ICP-Brasil (ver disclosure em Apr.cs).
export function AprAssinaturasTab({ aprId }: { aprId: string }) {
  const estilos = usePageStyles();
  const [assinaturas, setAssinaturas] = useState<AprAssinatura[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaAssinatura, setNovaAssinatura] = useState<NovaAprAssinatura>(() => assinaturaVazia(aprId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [assins, trabs] = await Promise.all([api.aprAssinaturas.listar(aprId), api.trabalhadores.listar()]);
      setAssinaturas(assins);
      setTrabalhadores(trabs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar assinaturas.');
    }
  }

  useEffect(() => {
    carregar();
    setNovaAssinatura(assinaturaVazia(aprId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aprId]);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.aprAssinaturas.criar(novaAssinatura);
      setNovaAssinatura(assinaturaVazia(aprId));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar assinatura.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Assinaturas / ciência</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Trabalhador">
          <Select
            value={novaAssinatura.trabalhadorId}
            onChange={(_, d) => setNovaAssinatura({ ...novaAssinatura, trabalhadorId: d.value })}
          >
            <option value="">Selecione</option>
            {trabalhadores.map((trabalhador) => (
              <option key={trabalhador.id} value={trabalhador.id}>
                {trabalhador.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Papel">
          <Select
            value={novaAssinatura.papel}
            onChange={(_, d) => setNovaAssinatura({ ...novaAssinatura, papel: Number(d.value) })}
          >
            {Object.entries(papelAssinaturaAprLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Registrar assinatura
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Trabalhador</TableHeaderCell>
            <TableHeaderCell>Papel</TableHeaderCell>
            <TableHeaderCell>Data</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {assinaturas.map((assinatura) => (
            <TableRow key={assinatura.id}>
              <TableCell>{assinatura.trabalhadorNome}</TableCell>
              <TableCell>{papelAssinaturaAprLabel[assinatura.papel]}</TableCell>
              <TableCell>{assinatura.dataAssinatura?.slice(0, 10)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
