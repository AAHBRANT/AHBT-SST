import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  StatusChip,
  type Coluna,
  type Tom,
} from '@ui';
import { ArrowDownload24Regular, Filter24Regular, Link24Regular } from '@fluentui/react-icons';
import { api, statusDocumentoAssinaturaLabel, type DocumentoAssinaturaResumo } from '../../lib/api';

// Status do Motor de Assinatura Eletrônica não tinha mapeamento de cor no Badge original (só
// appearance="tint" genérico) — julgamento novo desta conversão (Guia item 5): Em andamento = ainda
// não decidido (info), Finalizado = ok, Cancelado = alerta, mesmo padrão de "Cancelado→alerta" já
// usado em StatusPcmsoDocumento (Task 12).
const tomPorStatusAssinatura: Record<number, Tom> = { 1: 'info', 2: 'ok', 3: 'alerta' };

// Painel administrativo do Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5,
// etapa 12) — mesmo template de TrilhaAuditoriaTab (filtro + tabela), sem aba própria no menu (padrão
// "IA consolidada": funcionalidade nova vira aba dentro de Administração, não item novo de sidebar).
// Onda 2 Task 17 (camada ui/).
export function PainelAssinaturasTab() {
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
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

  const colunas: Coluna<DocumentoAssinaturaResumo>[] = [
    { chave: 'entidade', rotulo: 'Entidade', render: (d) => `${d.entidadeTipo} (${d.entidadeId})` },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (d) => (
        <StatusChip tom={tomPorStatusAssinatura[d.status] ?? 'neutro'}>
          {statusDocumentoAssinaturaLabel[d.status] ?? 'Desconhecido'}
        </StatusChip>
      ),
    },
    { chave: 'criadoEm', rotulo: 'Criado em', render: (d) => new Date(d.criadoEm).toLocaleString('pt-BR') },
    {
      chave: 'finalizadoEm',
      rotulo: 'Finalizado em',
      render: (d) => (d.finalizadoEm ? new Date(d.finalizadoEm).toLocaleString('pt-BR') : '—'),
    },
    { chave: 'assinaturas', rotulo: 'Assinaturas', render: (d) => d.quantidadeSignatarios },
  ];

  return (
    <div>
      <PageHeader titulo="Painel de assinaturas" />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card>
        <FormSection titulo="Filtros" primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Tipo de entidade">
                <Input value={entidadeTipo} onChange={(_, d) => setEntidadeTipo(d.value)} placeholder="Ex.: Dds" />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data início">
                <CampoData value={dataInicio} onChange={(_, d) => setDataInicio(d.value)} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data fim">
                <CampoData value={dataFim} onChange={(_, d) => setDataFim(d.value)} />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<Filter24Regular />} onClick={carregar} disabled={carregando}>
              Filtrar
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="Documentos de assinatura eletrônica"
          colunas={colunas}
          linhas={documentos}
          chaveLinha={(d) => d.id}
          carregando={carregando}
          vazio={{ titulo: 'Nenhum documento de assinatura encontrado para os filtros selecionados.' }}
          acoesLinha={(documento) => (
            <>
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
            </>
          )}
        />
      </Card>
    </div>
  );
}
