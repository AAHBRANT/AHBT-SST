import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Campo,
  Card,
  CampoData,
  ChipCheckboxGroup,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
  StatusChip,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  statusPtLabel,
  type Atividade,
  type Equipe,
  type NovaPermissaoTrabalho,
  type PermissaoTrabalho,
  type Trabalhador,
  type Usuario,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const ptVazia: NovaPermissaoTrabalho = {
  atividadeId: '',
  descricaoAtividade: '',
  local: '',
  empresaExecutante: '',
  equipeId: null,
  data: '',
  horarioInicio: null,
  horarioFim: null,
  validade: null,
  responsavelExecucaoUsuarioId: null,
  responsavelAreaUsuarioId: null,
  responsaveisIds: [],
};

// Mapeamento 1:1 pelo nome semântico do Fluent (Guia de conversão item 5), preservando as mesmas
// cores da versão anterior (Badge color=): subtle→neutro, success→ok, warning→atencao, informative→info.
const tomPorStatusPt: Record<number, Tom> = {
  1: 'neutro',
  2: 'ok',
  3: 'atencao',
  4: 'info',
};

// Onda 2 Task 6 (camada ui/): lista + formulário de criação de Permissão de Trabalho. Mesmo padrão
// de AprsTab.tsx (Task 11) — Card + FormSection + DataTable, seleção de responsáveis em ChipCheckboxGroup.
export function PermissoesTrabalhoTab() {
  const navigate = useNavigate();
  const [permissoes, setPermissoes] = useState<PermissaoTrabalho[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novaPt, setNovaPt] = useState<NovaPermissaoTrabalho>(ptVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, ativs, trabs, equips, usrs] = await Promise.all([
        api.permissoesTrabalho.listar(),
        api.atividades.listar(),
        api.trabalhadores.listar(),
        api.equipes.listar(),
        api.usuarios.listar(),
      ]);
      setPermissoes(lista);
      setAtividades(ativs);
      setTrabalhadores(trabs);
      setEquipes(equips);
      setUsuarios(usrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar Permissões de Trabalho.');
    } finally {
      setCarregandoLista(false);
    }
  }

  const obraIdAtividadeSelecionada = atividades.find((a) => a.id === novaPt.atividadeId)?.obraId;
  const equipesDaObra = obraIdAtividadeSelecionada
    ? equipes.filter((e) => e.obraId === obraIdAtividadeSelecionada)
    : equipes;

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.permissoesTrabalho.criar({
        ...novaPt,
        empresaExecutante: novaPt.empresaExecutante || null,
        equipeId: novaPt.equipeId || null,
        horarioInicio: novaPt.horarioInicio ? `${novaPt.horarioInicio}:00` : null,
        horarioFim: novaPt.horarioFim ? `${novaPt.horarioFim}:00` : null,
        validade: novaPt.validade || null,
        responsavelExecucaoUsuarioId: novaPt.responsavelExecucaoUsuarioId || null,
        responsavelAreaUsuarioId: novaPt.responsavelAreaUsuarioId || null,
      });
      setNovaPt(ptVazia);
      await carregar();
      sucessoToast('Permissão de Trabalho criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar Permissão de Trabalho.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta Permissão de Trabalho? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.permissoesTrabalho.excluir(id);
      await carregar();
      sucessoToast('Permissão de Trabalho excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir Permissão de Trabalho.');
    }
  }

  const colunas: Coluna<PermissaoTrabalho>[] = [
    { chave: 'numeroPt', rotulo: 'Nº PT', render: (pt) => pt.numeroPt ?? '-' },
    { chave: 'atividadeNome', rotulo: 'Atividade' },
    { chave: 'local', rotulo: 'Local' },
    { chave: 'data', rotulo: 'Data', render: (pt) => pt.data?.slice(0, 10) ?? '' },
    { chave: 'validade', rotulo: 'Validade', render: (pt) => pt.validade?.slice(0, 10) ?? '' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (pt) => <StatusChip tom={tomPorStatusPt[pt.status] ?? 'neutro'}>{statusPtLabel[pt.status]}</StatusChip>,
    },
  ];

  return (
    <>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card titulo="Permissão de Trabalho (PT)">
        <FormSection titulo="Dados Gerais" numero={1} primeira>
          <FormGrid>
            <Campo span={3}>
              <Field label="Atividade">
                <Select value={novaPt.atividadeId} onChange={(_, d) => setNovaPt({ ...novaPt, atividadeId: d.value })}>
                  <option value="">Selecione</option>
                  {atividades.map((atividade) => (
                    <option key={atividade.id} value={atividade.id}>
                      {atividade.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Descrição da atividade">
                <Input
                  value={novaPt.descricaoAtividade}
                  onChange={(_, d) => setNovaPt({ ...novaPt, descricaoAtividade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Local">
                <Input value={novaPt.local} onChange={(_, d) => setNovaPt({ ...novaPt, local: d.value })} />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Empresa executante">
                <Input
                  value={novaPt.empresaExecutante ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, empresaExecutante: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Equipe">
                <Select
                  value={novaPt.equipeId ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, equipeId: d.value || null })}
                >
                  <option value="">Nenhuma</option>
                  {equipesDaObra.map((equipe) => (
                    <option key={equipe.id} value={equipe.id}>
                      {equipe.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>

        <FormSection titulo="Prazos e Responsáveis" numero={2}>
          <FormGrid>
            <Campo span={2}>
              <Field label="Data">
                <CampoData value={novaPt.data} onChange={(_, d) => setNovaPt({ ...novaPt, data: d.value })} />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Horário início">
                <Input
                  type="time"
                  value={novaPt.horarioInicio ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, horarioInicio: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Horário fim">
                <Input
                  type="time"
                  value={novaPt.horarioFim ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, horarioFim: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Validade">
                <CampoData
                  value={novaPt.validade ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, validade: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Responsável pela execução">
                <Select
                  value={novaPt.responsavelExecucaoUsuarioId ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, responsavelExecucaoUsuarioId: d.value || null })}
                >
                  <option value="">Não definido</option>
                  {usuarios.map((usuario) => (
                    <option key={usuario.id} value={usuario.id}>
                      {usuario.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Responsável pela área">
                <Select
                  value={novaPt.responsavelAreaUsuarioId ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, responsavelAreaUsuarioId: d.value || null })}
                >
                  <option value="">Não definido</option>
                  {usuarios.map((usuario) => (
                    <option key={usuario.id} value={usuario.id}>
                      {usuario.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Equipe executante (responsáveis)">
                <ChipCheckboxGroup
                  aria-label="Equipe executante (responsáveis)"
                  opcoes={trabalhadores.map((t) => ({ id: t.id, rotulo: t.nome }))}
                  selecionados={novaPt.responsaveisIds}
                  aoMudar={(ids) => setNovaPt({ ...novaPt, responsaveisIds: ids })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button
              appearance="primary"
              icon={<Add24Regular />}
              onClick={criar}
              disabled={carregando || !novaPt.atividadeId || !novaPt.descricaoAtividade || !novaPt.local || !novaPt.data}
            >
              Adicionar PT
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="Permissões de Trabalho cadastradas"
          colunas={colunas}
          linhas={permissoes}
          chaveLinha={(pt) => pt.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhuma Permissão de Trabalho cadastrada ainda.' }}
          aoClicarLinha={(pt) => navigate(`/operacao/pt/${pt.id}`)}
          acoesLinha={(pt) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(pt.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      {dialogElement}
    </>
  );
}
