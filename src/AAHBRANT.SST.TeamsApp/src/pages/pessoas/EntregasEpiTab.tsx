import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { ArrowDownload24Regular, Open24Regular } from '@fluentui/react-icons';
import { api, type CatalogoEpi, type EntregaEpi } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

// Histórico somente-leitura das entregas de EPI deste trabalhador. O registro de novas entregas,
// devoluções e a assinatura da ficha passaram a viver no módulo dedicado /epi (sidebar fixa "EPI",
// decisão confirmada com o usuário) — aqui fica só a consulta, com atalho para lá.
export function EntregasEpiTab({ trabalhadorId }: { trabalhadorId: string }) {
  const estilos = usePageStyles();
  const navigate = useNavigate();
  const [entregas, setEntregas] = useState<EntregaEpi[]>([]);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [baixando, setBaixando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaEpis] = await Promise.all([
        api.entregasEpi.listar(trabalhadorId),
        api.catalogosEpi.listar(),
      ]);
      setEntregas(lista);
      setEpis(listaEpis);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar entregas de EPI.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  function nomeEpi(id: string) {
    return epis.find((e) => e.id === id)?.nome ?? id;
  }

  function vencido(dataValidade?: string | null) {
    if (!dataValidade) return false;
    return new Date(dataValidade) < new Date(new Date().toDateString());
  }

  async function baixarFicha() {
    try {
      setBaixando(true);
      const blob = await api.entregasEpi.baixarFichaTrabalhador(trabalhadorId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ficha-epi-${trabalhadorId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a ficha em PDF.');
    } finally {
      setBaixando(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Entregas de EPI do trabalhador</Text>
        <div style={{ display: 'flex', gap: 8 }}>
          <Button
            appearance="subtle"
            icon={<ArrowDownload24Regular />}
            onClick={baixarFicha}
            disabled={baixando || entregas.length === 0}
          >
            Baixar ficha (PDF)
          </Button>
          <Button appearance="primary" icon={<Open24Regular />} onClick={() => navigate('/epi')}>
            Registrar nova entrega
          </Button>
        </div>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>EPI</TableHeaderCell>
            <TableHeaderCell>Qtd.</TableHeaderCell>
            <TableHeaderCell>Entrega</TableHeaderCell>
            <TableHeaderCell>Validade</TableHeaderCell>
            <TableHeaderCell>Devolução</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {entregas.map((entrega) => (
            <TableRow key={entrega.id}>
              <TableCell>{nomeEpi(entrega.catalogoEpiId)}</TableCell>
              <TableCell>{entrega.quantidade}</TableCell>
              <TableCell>{entrega.dataEntrega?.slice(0, 10)}</TableCell>
              <TableCell>
                {entrega.dataValidade?.slice(0, 10)}
                {vencido(entrega.dataValidade) && !entrega.dataDevolucao && (
                  <Badge color="danger" appearance="tint" style={{ marginLeft: 8 }}>
                    Vencido
                  </Badge>
                )}
              </TableCell>
              <TableCell>{entrega.dataDevolucao?.slice(0, 10)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      {entregas.length === 0 && <Text style={{ display: 'block', marginTop: 8 }}>Nenhuma entrega de EPI registrada.</Text>}
    </div>
  );
}
