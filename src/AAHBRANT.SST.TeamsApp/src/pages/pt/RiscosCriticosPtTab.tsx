import { useState } from 'react';
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
  Input,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type NovaPermissaoTrabalhoRiscoCritico, type PermissaoTrabalhoRiscoCritico } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function riscoVazio(permissaoTrabalhoId: string): NovaPermissaoTrabalhoRiscoCritico {
  return { permissaoTrabalhoId, riscoCondicao: '', controleComplementar: '', responsavelEvidencia: '' };
}

// §6 do formulário, "Riscos críticos e controles complementares" — tabela livre, sem catálogo fixo.
export function RiscosCriticosPtTab({
  permissaoTrabalhoId,
  itens,
  aoAtualizar,
}: {
  permissaoTrabalhoId: string;
  itens: PermissaoTrabalhoRiscoCritico[];
  aoAtualizar: () => Promise<void>;
}) {
  const [novoRisco, setNovoRisco] = useState<NovaPermissaoTrabalhoRiscoCritico>(() =>
    riscoVazio(permissaoTrabalhoId),
  );
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function criar() {
    if (!novoRisco.riscoCondicao.trim()) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.permissoesTrabalho.criarRiscoCritico({
        ...novoRisco,
        controleComplementar: novoRisco.controleComplementar || null,
        responsavelEvidencia: novoRisco.responsavelEvidencia || null,
      });
      setNovoRisco(riscoVazio(permissaoTrabalhoId));
      await aoAtualizar();
      sucessoToast('Risco crítico adicionado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao adicionar risco crítico.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este risco crítico? Essa ação não pode ser desfeita.'))) return;
    try {
      setErro(null);
      await api.permissoesTrabalho.excluirRiscoCritico(id);
      await aoAtualizar();
      sucessoToast('Risco crítico excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir risco crítico.');
    }
  }

  const colunas: Coluna<PermissaoTrabalhoRiscoCritico>[] = [
    { chave: 'riscoCondicao', rotulo: 'Risco / condição' },
    { chave: 'controleComplementar', rotulo: 'Controle complementar', render: (item) => item.controleComplementar ?? '-' },
    { chave: 'responsavelEvidencia', rotulo: 'Responsável / evidência', render: (item) => item.responsavelEvidencia ?? '-' },
  ];

  return (
    <>
      <Card titulo="Riscos críticos / controles complementares">
        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}

        <FormSection titulo="Dados do Risco Crítico" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Risco / condição">
                <Input
                  value={novoRisco.riscoCondicao}
                  onChange={(_, d) => setNovoRisco({ ...novoRisco, riscoCondicao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Controle complementar">
                <Input
                  value={novoRisco.controleComplementar ?? ''}
                  onChange={(_, d) => setNovoRisco({ ...novoRisco, controleComplementar: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Responsável / evidência">
                <Input
                  value={novoRisco.responsavelEvidencia ?? ''}
                  onChange={(_, d) => setNovoRisco({ ...novoRisco, responsavelEvidencia: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Adicionar risco crítico
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="Riscos críticos cadastrados"
          colunas={colunas}
          linhas={itens}
          chaveLinha={(item) => item.id}
          vazio={{ titulo: 'Nenhum risco crítico cadastrado ainda.' }}
          acoesLinha={(item) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(item.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      {dialogElement}
    </>
  );
}
