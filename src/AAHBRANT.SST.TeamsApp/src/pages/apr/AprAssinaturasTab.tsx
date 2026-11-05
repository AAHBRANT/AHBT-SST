import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { BotaoAcao, Button, Campo, Card, DataTable, Field, FeedbackInline, FormGrid, FormRodape, FormSection, Select, type Coluna } from '@ui';
import { Fingerprint24Regular, Save24Regular } from '@fluentui/react-icons';
import { api, PapelAssinaturaApr, type AprAssinatura, type AprDetalhe, type Trabalhador } from '../../lib/api';

// APR por biometria (05/11, pedido do usuário): todo mundo — elaboração, supervisão e envolvidos — assina
// com a digital ou o reconhecimento facial no quiosque. O antigo registro de ciência por clique saiu da tela;
// os registros antigos continuam na lista e no PDF, marcados como "sem biometria".
interface LinhaAssinatura {
  chave: string;
  nome: string;
  papel: string;
  assinatura?: AprAssinatura;
}

const metodoRotulo: Record<number, string> = { 1: 'digital', 6: 'facial', 5: 'login' };

function situacao(a?: AprAssinatura) {
  if (!a) return <span style={{ color: '#a86b00', fontWeight: 600 }}>Aguardando assinatura</span>;
  const quando = new Date(a.dataAssinatura).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
  if (!a.biometrica) return <span style={{ color: '#6f6666' }}>Ciência registrada (sem biometria) · {quando}</span>;
  return (
    <span style={{ color: '#2e7d4f', fontWeight: 600 }}>
      ✔ Assinado · {metodoRotulo[a.metodoAutenticacao ?? 0] ?? 'biometria'} · {quando}
    </span>
  );
}

export function AprAssinaturasTab({ aprId }: { aprId: string }) {
  const navigate = useNavigate();
  const [detalhe, setDetalhe] = useState<AprDetalhe | null>(null);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [elaborador, setElaborador] = useState('');
  const [supervisor, setSupervisor] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [carregando, setCarregando] = useState(true);

  async function carregar() {
    try {
      setErro(null);
      const [d, trabs] = await Promise.all([api.aprs.obterDetalhe(aprId), api.trabalhadores.listar()]);
      setDetalhe(d);
      setTrabalhadores(trabs);
      setElaborador(d.apr.elaboradorTrabalhadorId ?? '');
      setSupervisor(d.apr.supervisorTrabalhadorId ?? '');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar assinaturas.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [aprId]);

  async function salvarResponsaveis() {
    try {
      setSalvando(true);
      setErro(null);
      await api.aprs.definirResponsaveisAssinatura(aprId, elaborador || null, supervisor || null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar quem elabora e supervisiona.');
    } finally {
      setSalvando(false);
    }
  }

  // Assinatura mais recente por pessoa e papel; a biométrica vale mais que o registro antigo por clique.
  const linhas = useMemo<LinhaAssinatura[]>(() => {
    if (!detalhe) return [];
    const assinaturaDe = (trabalhadorId: string | null | undefined, papel: number) =>
      detalhe.assinaturas
        .filter((a) => a.trabalhadorId === trabalhadorId && a.papel === papel)
        .sort((x, y) => Number(!!y.biometrica) - Number(!!x.biometrica) || y.dataAssinatura.localeCompare(x.dataAssinatura))[0];
    const resultado: LinhaAssinatura[] = [];
    if (detalhe.apr.elaboradorTrabalhadorId)
      resultado.push({ chave: 'elab', nome: detalhe.apr.elaboradorNome ?? '—', papel: 'Elaboração', assinatura: assinaturaDe(detalhe.apr.elaboradorTrabalhadorId, PapelAssinaturaApr.Elaboracao) });
    if (detalhe.apr.supervisorTrabalhadorId)
      resultado.push({ chave: 'sup', nome: detalhe.apr.supervisorNome ?? '—', papel: 'Supervisão', assinatura: assinaturaDe(detalhe.apr.supervisorTrabalhadorId, PapelAssinaturaApr.Supervisao) });
    for (const r of detalhe.responsaveis)
      resultado.push({ chave: r.id, nome: r.trabalhadorNome, papel: 'Envolvido', assinatura: assinaturaDe(r.trabalhadorId, PapelAssinaturaApr.Envolvido) });
    return resultado;
  }, [detalhe]);

  const colunas: Coluna<LinhaAssinatura>[] = [
    { chave: 'nome', rotulo: 'Pessoa' },
    { chave: 'papel', rotulo: 'Papel' },
    { chave: 'assinatura', rotulo: 'Situação', render: (l) => situacao(l.assinatura) },
  ];

  const opcoes = trabalhadores.map((t) => (
    <option key={t.id} value={t.id}>
      {t.nome}
    </option>
  ));

  return (
    <Card titulo="Assinaturas">
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <FormSection titulo="Responsáveis pela APR" numero={1} primeira>
        <FormGrid>
          <Campo span={6}>
            <Field label="Elaboração (técnico / encarregado)">
              <Select value={elaborador} onChange={(_, d) => setElaborador(d.value)}>
                <option value="">Selecione</option>
                {opcoes}
              </Select>
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Supervisão">
              <Select value={supervisor} onChange={(_, d) => setSupervisor(d.value)}>
                <option value="">Selecione</option>
                {opcoes}
              </Select>
            </Field>
          </Campo>
        </FormGrid>
        <FormRodape>
          <Button appearance="primary" icon={<Save24Regular />} onClick={salvarResponsaveis} disabled={salvando}>
            Salvar responsáveis
          </Button>
        </FormRodape>
      </FormSection>

      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', margin: '16px 0' }}>
        <span>Cada pessoa assina com a digital (leitor) ou o reconhecimento facial.</span>
        <BotaoAcao
          tom="assinar"
          icon={<Fingerprint24Regular />}
          onClick={() => navigate(`/operacao/apr/${aprId}/assinar`)}
          aria-label="Assinar com digital ou facial"
        >
          Assinar com digital ou facial
        </BotaoAcao>
      </div>

      <DataTable
        aria-label="Assinaturas da APR"
        colunas={colunas}
        linhas={linhas}
        chaveLinha={(l) => l.chave}
        carregando={carregando}
        vazio={{ titulo: 'Defina quem elabora, quem supervisiona e os envolvidos para colher as assinaturas.' }}
      />
    </Card>
  );
}
