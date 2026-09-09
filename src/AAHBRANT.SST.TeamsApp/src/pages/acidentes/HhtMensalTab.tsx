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
  Input,
  Select,
  useConfirmar,
  type Coluna,
} from '@ui';
import { AddCircle24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type NovoRegistroHhtMensal, type Obra, type RegistroHhtMensal } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const nomesMes = [
  'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
];

function novoInicial(): NovoRegistroHhtMensal {
  const agora = new Date();
  return { obraId: '', ano: agora.getFullYear(), mes: agora.getMonth() + 1, horasHomemTrabalhadas: 0 };
}

// Onda 2 Task 20 (camada ui/): lançamento + histórico de HHT mensal por obra, aba de AcidentesPage.
export function HhtMensalTab({ obras }: { obras: Obra[] }) {
  const [registros, setRegistros] = useState<RegistroHhtMensal[]>([]);
  const [novo, setNovo] = useState<NovoRegistroHhtMensal>(novoInicial());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setRegistros(await api.registrosHht.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar registros de HHT.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    if (!novo.obraId) {
      setErro('Selecione a obra.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.registrosHht.criar(novo);
      setNovo(novoInicial());
      await carregar();
      sucessoToast('HHT lançado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar HHT.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este registro de HHT? Essa ação não pode ser desfeita.'))) return;
    try {
      setErro(null);
      await api.registrosHht.excluir(id);
      await carregar();
      sucessoToast('Registro de HHT excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir registro.');
    }
  }

  const colunas: Coluna<RegistroHhtMensal>[] = [
    { chave: 'obra', rotulo: 'Obra', render: (r) => r.obraNome ?? '—' },
    { chave: 'ano', rotulo: 'Ano' },
    { chave: 'mes', rotulo: 'Mês', render: (r) => nomesMes[r.mes - 1] },
    { chave: 'hht', rotulo: 'HHT', render: (r) => r.horasHomemTrabalhadas.toLocaleString('pt-BR') },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card titulo="Lançar HHT do mês">
        <FormSection titulo="Dados do Lançamento" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Obra" required>
                <Select value={novo.obraId} onChange={(_, d) => setNovo({ ...novo, obraId: d.value })}>
                  <option value="">Selecione</option>
                  {obras.map((obra) => (
                    <option key={obra.id} value={obra.id}>
                      {obra.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Ano" required>
                <Input
                  type="number"
                  value={String(novo.ano)}
                  onChange={(_, d) => setNovo({ ...novo, ano: Number(d.value) || novo.ano })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Mês" required>
                <Select value={String(novo.mes)} onChange={(_, d) => setNovo({ ...novo, mes: Number(d.value) })}>
                  {nomesMes.map((nome, indice) => (
                    <option key={nome} value={indice + 1}>
                      {nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Horas-Homem Trabalhadas (HHT)" required>
                <Input
                  type="number"
                  min={0}
                  value={String(novo.horasHomemTrabalhadas)}
                  onChange={(_, d) => setNovo({ ...novo, horasHomemTrabalhadas: Number(d.value) || 0 })}
                />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
        <FormRodape>
          <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={carregando}>
            Lançar
          </Button>
        </FormRodape>
      </Card>

      <Card titulo="Histórico de HHT por obra">
        <DataTable
          aria-label="Histórico de HHT por obra"
          colunas={colunas}
          linhas={registros}
          chaveLinha={(r) => r.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhum registro de HHT cadastrado ainda.' }}
          acoesLinha={(r) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(r.id)} aria-label="Excluir" />
          )}
        />
      </Card>
    </div>
  );
}
