import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Select,
  type Coluna,
} from '@ui';
import { Add24Regular } from '@fluentui/react-icons';
import {
  api,
  papelAssinaturaAprLabel,
  PapelAssinaturaApr,
  type AprAssinatura,
  type NovaAprAssinatura,
  type Trabalhador,
} from '../../lib/api';

function assinaturaVazia(aprId: string): NovaAprAssinatura {
  return { aprId, trabalhadorId: '', papel: PapelAssinaturaApr.Envolvido };
}

// Onda 2 Task 11 (camada ui/): assinatura (§17) é registro de ciência append-only — sem edição/
// exclusão, mesmo padrão de PgrRevisoesTab. Não é assinatura criptográfica/ICP-Brasil (ver disclosure
// em Apr.cs).
export function AprAssinaturasTab({ aprId }: { aprId: string }) {
  const [assinaturas, setAssinaturas] = useState<AprAssinatura[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [novaAssinatura, setNovaAssinatura] = useState<NovaAprAssinatura>(() => assinaturaVazia(aprId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);

  async function carregar() {
    try {
      setErro(null);
      const [assins, trabs] = await Promise.all([api.aprAssinaturas.listar(aprId), api.trabalhadores.listar()]);
      setAssinaturas(assins);
      setTrabalhadores(trabs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar assinaturas.');
    } finally {
      setCarregandoLista(false);
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

  const colunas: Coluna<AprAssinatura>[] = [
    { chave: 'trabalhadorNome', rotulo: 'Funcionário' },
    { chave: 'papel', rotulo: 'Papel', render: (a) => papelAssinaturaAprLabel[a.papel] },
    { chave: 'dataAssinatura', rotulo: 'Data', render: (a) => a.dataAssinatura?.slice(0, 10) ?? '' },
  ];

  return (
    <Card titulo="Assinaturas / ciência">
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <FormSection titulo="Nova assinatura" numero={1} primeira>
        <FormGrid>
          <Campo span={4}>
            <Field label="Funcionário">
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
          </Campo>
          <Campo span={3}>
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
          </Campo>
        </FormGrid>
        <FormRodape>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Registrar assinatura
          </Button>
        </FormRodape>
      </FormSection>

      <DataTable
        aria-label="Assinaturas registradas"
        colunas={colunas}
        linhas={assinaturas}
        chaveLinha={(a) => a.id}
        carregando={carregandoLista}
        vazio={{ titulo: 'Nenhuma assinatura registrada ainda.' }}
      />
    </Card>
  );
}
