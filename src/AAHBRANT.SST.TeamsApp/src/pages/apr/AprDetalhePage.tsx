import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Abas,
  Button,
  Campo,
  Card,
  Carregando,
  DetailPageLayout,
  Field,
  FeedbackInline,
  FormGrid,
  Input,
  StatusChip,
  Textarea,
  WorkflowActions,
  type AcaoWorkflow,
  type Tom,
} from '@ui';
import { ArrowDownload24Regular } from '@fluentui/react-icons';
import { api, StatusApr, statusAprLabel, type AprDetalhe } from '../../lib/api';
import { AprAssinaturasTab } from './AprAssinaturasTab';
import { AprEtapasTab } from './AprEtapasTab';

const ABAS_APR = ['etapas', 'assinaturas'] as const;
type AbaApr = (typeof ABAS_APR)[number];

// Mesmo mapeamento de AprsTab.tsx, repetido aqui para lista e detalhe ficarem visualmente coerentes
// (ruling da Onda 2: mesmos tons de StatusChip entre as duas telas do módulo).
const tomPorStatusApr: Record<number, Tom> = {
  [StatusApr.EmElaboracao]: 'neutro',
  [StatusApr.AguardandoAprovacao]: 'atencao',
  [StatusApr.Aprovada]: 'ok',
  [StatusApr.Reprovada]: 'alerta',
  [StatusApr.Encerrada]: 'info',
};

// Onda 2 Task 11 (camada ui/): detalhe e decisão (aprovar/reprovar) de uma APR. DetailPageLayout com
// resumo e ações do fluxo na lateral (mesmo padrão de NaoConformidadeDetalhePage, piloto 2) — as
// abas internas (Etapas/Assinaturas) usam estado local, sem useAbaNaUrl, porque já vivem dentro de
// uma página endereçada por :id.
export function AprDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [aba, setAba] = useState<AbaApr>('etapas');
  const [detalhe, setDetalhe] = useState<AprDetalhe | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [aprovadoPorUsuarioId, setAprovadoPorUsuarioId] = useState('');
  const [motivoReprovacao, setMotivoReprovacao] = useState('');
  const [processando, setProcessando] = useState(false);
  const [exportando, setExportando] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setDetalhe(await api.aprs.obterDetalhe(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar APR.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  // As duas ações abaixo entram no WorkflowActions e seguem o contrato dele (piloto 2, spec):
  // `false` mantém o formulário aberto — tanto a guarda local (campo vazio) quanto a falha da API
  // devolvem `false`, senão o acordeão fecharia e a mensagem de erro ficaria fora da vista.
  async function aprovar() {
    if (!id || !aprovadoPorUsuarioId) return false;
    try {
      setProcessando(true);
      setErro(null);
      await api.aprs.aprovar(id, aprovadoPorUsuarioId);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao aprovar APR.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function reprovar() {
    if (!id || !motivoReprovacao) return false;
    try {
      setProcessando(true);
      setErro(null);
      await api.aprs.reprovar(id, motivoReprovacao);
      setMotivoReprovacao('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao reprovar APR.');
      return false;
    } finally {
      setProcessando(false);
    }
  }

  async function exportarPdf() {
    if (!id) return;
    try {
      setExportando(true);
      setErro(null);
      const blob = await api.aprs.exportarPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `apr-${id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao exportar PDF da APR.');
    } finally {
      setExportando(false);
    }
  }

  if (!id) return <FeedbackInline tom="erro">APR não encontrada.</FeedbackInline>;

  // Erro na carga inicial precisa aparecer aqui: sem isso o skeleton ficaria para sempre e a falha
  // (API fora, id inexistente) não teria onde ser lida.
  if (!detalhe) {
    return erro ? (
      <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void carregar() }}>
        {erro}
      </FeedbackInline>
    ) : (
      <Carregando variante="detalhe" linhas={8} />
    );
  }

  const podeDecidir =
    detalhe.apr.status === StatusApr.AguardandoAprovacao || detalhe.apr.status === StatusApr.EmElaboracao;

  const acoes: AcaoWorkflow[] = [];
  if (podeDecidir) {
    acoes.push({
      chave: 'aprovar',
      rotulo: 'Aprovar',
      tom: 'primario',
      habilitada: true,
      aoExecutar: aprovar,
      formulario: (
        <Field label="ID do usuário aprovador (GUID)" required>
          <Input value={aprovadoPorUsuarioId} onChange={(_, d) => setAprovadoPorUsuarioId(d.value)} />
        </Field>
      ),
    });
    acoes.push({
      chave: 'reprovar',
      rotulo: 'Reprovar',
      tom: 'destrutivo',
      habilitada: true,
      aoExecutar: reprovar,
      formulario: (
        <Field label="Motivo da reprovação" required>
          <Textarea value={motivoReprovacao} onChange={(_, d) => setMotivoReprovacao(d.value)} />
        </Field>
      ),
    });
  }

  return (
    <DetailPageLayout
      cabecalho={{
        titulo: `${detalhe.apr.numeroApr ? detalhe.apr.numeroApr + ' — ' : ''}${detalhe.apr.atividadeNome}`,
        status: (
          <StatusChip tom={tomPorStatusApr[detalhe.apr.status] ?? 'neutro'}>
            {statusAprLabel[detalhe.apr.status]}
          </StatusChip>
        ),
        voltarPara: '/operacao/apr',
        rotuloVoltar: 'Voltar para APR',
        acoes: (
          <Button appearance="secondary" icon={<ArrowDownload24Regular />} onClick={exportarPdf} disabled={exportando}>
            Exportar PDF
          </Button>
        ),
      }}
      lateral={
        <>
          <Card densidade="compacta" titulo="Resumo">
            <FormGrid>
              {detalhe.apr.obraNome && (
                <Campo span={12}>
                  <Field label="Obra">
                    <Input value={detalhe.apr.obraNome} readOnly />
                  </Field>
                </Campo>
              )}
              <Campo span={12}>
                <Field label="Local">
                  <Input value={detalhe.apr.local} readOnly />
                </Field>
              </Campo>
              <Campo span={12}>
                <Field label="Data">
                  <Input value={detalhe.apr.data?.slice(0, 10) ?? ''} readOnly />
                </Field>
              </Campo>
              {detalhe.apr.validade && (
                <Campo span={12}>
                  <Field label="Validade">
                    <Input value={detalhe.apr.validade.slice(0, 10)} readOnly />
                  </Field>
                </Campo>
              )}
              {detalhe.apr.maquinasEquipamentos && (
                <Campo span={12}>
                  <Field label="Máquinas / Equip.">
                    <Input value={detalhe.apr.maquinasEquipamentos} readOnly />
                  </Field>
                </Campo>
              )}
              {detalhe.apr.pgrReferencia && (
                <Campo span={12}>
                  <Field label="PGR / Procedimento ref.">
                    <Input value={detalhe.apr.pgrReferencia} readOnly />
                  </Field>
                </Campo>
              )}
              {detalhe.responsaveis.length > 0 && (
                <Campo span={12}>
                  <Field label="Responsáveis">
                    <Input value={detalhe.responsaveis.map((r) => r.trabalhadorNome).join(', ')} readOnly />
                  </Field>
                </Campo>
              )}
              {detalhe.apr.status === StatusApr.Aprovada && (
                <Campo span={12}>
                  <Field label="Aprovação">
                    <Input
                      value={`Aprovada por ${detalhe.apr.aprovadoPorUsuarioNome ?? detalhe.apr.aprovadoPorUsuarioId} em ${detalhe.apr.dataAprovacao?.slice(0, 10) ?? ''}`}
                      readOnly
                    />
                  </Field>
                </Campo>
              )}
              {detalhe.apr.status === StatusApr.Reprovada && detalhe.apr.motivoReprovacao && (
                <Campo span={12}>
                  <Field label="Motivo da reprovação">
                    <Input value={detalhe.apr.motivoReprovacao} readOnly />
                  </Field>
                </Campo>
              )}
            </FormGrid>
          </Card>
          {acoes.length > 0 && (
            <Card densidade="compacta" titulo="Ações disponíveis">
              <WorkflowActions acoes={acoes} processando={processando} />
            </Card>
          )}
        </>
      }
    >
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Abas
        nivel="interno"
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções da APR"
        abas={[
          { valor: 'etapas', rotulo: 'Etapas' },
          { valor: 'assinaturas', rotulo: 'Assinaturas' },
        ]}
      />

      {aba === 'etapas' && <AprEtapasTab aprId={id} />}
      {aba === 'assinaturas' && <AprAssinaturasTab aprId={id} />}
    </DetailPageLayout>
  );
}
