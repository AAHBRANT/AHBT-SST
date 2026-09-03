import { useState } from 'react';
import {
  Button,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { ArrowDownload24Regular } from '@fluentui/react-icons';
import { api, metodoAutenticacaoAssinaturaLabel, type AssinaturaPerfil } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { AssinaturaTab } from './AssinaturaTab';

interface CofreAssinaturasTabProps {
  trabalhadorId: string;
  assinaturas: AssinaturaPerfil[];
}

export function CofreAssinaturasTab({ trabalhadorId, assinaturas }: CofreAssinaturasTabProps) {
  const estilos = usePageStyles();
  const [erro, setErro] = useState<string | null>(null);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);

  async function baixarComprovante(documentoAssinaturaId: string) {
    try {
      setErro(null);
      setBaixandoId(documentoAssinaturaId);
      const blob = await api.assinatura.baixarPdf(documentoAssinaturaId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `comprovante-assinatura-${documentoAssinaturaId}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar o comprovante em PDF.');
    } finally {
      setBaixandoId(null);
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Cofre de assinaturas</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        {assinaturas.length === 0 ? (
          <Text>Nenhuma assinatura registrada para este trabalhador.</Text>
        ) : (
          <Table noNativeElements>
            <TableHeader>
              <TableRow>
                <TableHeaderCell>Documento</TableHeaderCell>
                <TableHeaderCell>Método</TableHeaderCell>
                <TableHeaderCell>Data/Hora</TableHeaderCell>
                <TableHeaderCell>IP</TableHeaderCell>
                <TableHeaderCell></TableHeaderCell>
              </TableRow>
            </TableHeader>
            <TableBody>
              {assinaturas.map((assinatura) => (
                <TableRow key={assinatura.documentoAssinaturaId}>
                  <TableCell>{assinatura.entidadeTipo}</TableCell>
                  <TableCell>{metodoAutenticacaoAssinaturaLabel[assinatura.metodo]}</TableCell>
                  <TableCell>{new Date(assinatura.assinadoEm).toLocaleString('pt-BR')}</TableCell>
                  <TableCell>{assinatura.ipAddress ?? 'Não registrado'}</TableCell>
                  <TableCell>
                    <Button
                      appearance="subtle"
                      icon={<ArrowDownload24Regular />}
                      onClick={() => baixarComprovante(assinatura.documentoAssinaturaId)}
                      disabled={!assinatura.temPdf || baixandoId === assinatura.documentoAssinaturaId}
                      aria-label="Baixar comprovante"
                      title={assinatura.temPdf ? 'Baixar comprovante em PDF' : 'PDF ainda não disponível'}
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </div>

      <AssinaturaTab trabalhadorId={trabalhadorId} />
    </div>
  );
}
