import { useEffect, useState } from 'react';
import {
  Badge,
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
import { ArrowDownload24Regular, Filter24Regular, Link24Regular } from '@fluentui/react-icons';
import { api, statusDocumentoAssinaturaLabel, type DocumentoAssinaturaResumo } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

// Painel administrativo do Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5,
// etapa 12) — mesmo template de TrilhaAuditoriaTab (filtro + tabela), sem aba própria no menu (padrão
// "IA consolidada": funcionalidade nova vira aba dentro de Administração, não item novo de sidebar).
export function PainelAssinaturasTab() {
  const estilos = usePageStyles();
  const [documentos, setDocumentos] = useState<DocumentoAssinaturaResumo[]>([]);
  const [entidadeTipo, setEntidadeTipo] = useState('');
  const [dataInicio, setDataInicio] = useState('');
  const [dataFim, setDataFim] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [baixandoId, setBaixandoId] = useState<string | null>(null);

  async function carregar() {
    try {
      setCarregando(true);
      setErro(null);
      const dados = await api.assinatura.listar({
        entidadeTipo: entidadeTipo || undefined,
        dataInicio: dataInicio || undefined,
        dataFim: dataFim || undefined,
      });
      setDocumentos(dados);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar os documentos de assinatura.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function baixarPdf(documento: DocumentoAssinaturaResumo) {
    try {
      setBaixandoId(documento.id);
      setErro(null);
      const blob = await api.assinatura.baixarPdf(documento.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `comprovante-assinatura-${documento.id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar o PDF do documento.');
    } finally {
      setBaixandoId(null);
    }
  }

  async function copiarLinkPublico(documento: DocumentoAssinaturaResumo) {
    if (!documento.tokenValidacaoPublica) return;
    const url = `${window.location.origin}/#/validar/${documento.tokenValidacaoPublica}`;
    try {
      await navigator.clipboard.writeText(url);
    } catch {
      setErro('Não foi possível copiar o link — copie manualmente: ' + url);
    }
  }

  return (
    <div>
      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Painel de assinaturas</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Filtros</div>
        <div className={estilos.formGrid}>
          <div className={estilos.col4}>
            <Field label="Tipo de entidade">
              <Input value={entidadeTipo} onChange={(_, d) => setEntidadeTipo(d.value)} placeholder="Ex.: Dds" />
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Data início">
              <CampoData value={dataInicio} onChange={(_, d) => setDataInicio(d.value)} />
            </Field>
          </div>
          <div className={estilos.col3}>
            <Field label="Data fim">
              <CampoData value={dataFim} onChange={(_, d) => setDataFim(d.value)} />
            </Field>
          </div>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Filter24Regular />} onClick={carregar} disabled={carregando}>
            Filtrar
          </Button>
        </div>

        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Entidade</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Criado em</TableHeaderCell>
              <TableHeaderCell>Finalizado em</TableHeaderCell>
              <TableHeaderCell>Assinaturas</TableHeaderCell>
              <TableHeaderCell>Ações</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {documentos.map((documento) => (
              <TableRow key={documento.id}>
                <TableCell>
                  {documento.entidadeTipo} ({documento.entidadeId})
                </TableCell>
                <TableCell>
                  <Badge appearance="tint" size="small">
                    {statusDocumentoAssinaturaLabel[documento.status] ?? 'Desconhecido'}
                  </Badge>
                </TableCell>
                <TableCell>{new Date(documento.criadoEm).toLocaleString('pt-BR')}</TableCell>
                <TableCell>{documento.finalizadoEm ? new Date(documento.finalizadoEm).toLocaleString('pt-BR') : '—'}</TableCell>
                <TableCell>{documento.quantidadeSignatarios}</TableCell>
                <TableCell>
                  <div style={{ display: 'flex', gap: 8 }}>
                    {documento.temPdf && (
                      <Button
                        size="small"
                        icon={<ArrowDownload24Regular />}
                        onClick={() => baixarPdf(documento)}
                        disabled={baixandoId === documento.id}
                      >
                        PDF
                      </Button>
                    )}
                    {documento.tokenValidacaoPublica && (
                      <Button size="small" icon={<Link24Regular />} onClick={() => copiarLinkPublico(documento)}>
                        Link
                      </Button>
                    )}
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
