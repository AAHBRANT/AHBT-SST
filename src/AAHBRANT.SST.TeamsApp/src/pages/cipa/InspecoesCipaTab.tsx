import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  CampoData,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  Input,
  PageHeader,
  PainelLateral,
  Select,
  StatusChip,
  Textarea,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  api,
  nivelRiscoLabel,
  type InspecaoCipa,
  type MembroCipa,
  type NovaInspecaoCipa,
  type Obra,
  type Usuario,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function vazio(): NovaInspecaoCipa {
  return { obraId: '', membroCipaId: null, data: '', local: '', riscoIdentificado: '', grauRisco: null };
}

// Integração com PGR/GRO: este sistema NÃO envia alertas automáticos ao inventário de riscos do
// GRO. O botão "Gerar Não Conformidade" cria manualmente uma Não Conformidade (mesmo mecanismo de
// NaoConformidadesTab.tsx) a partir do risco identificado na inspeção — ver disclosure em Cipa.cs.
// Camada ui/ (Onda 2, Task 4): formulário de registro saiu de cima da tabela para um PainelLateral
// (Guia §2); o mini-formulário de "gerar NC" fica como Card contextual acima da lista, por ser
// disparado por linha e de vida curta (não é o formulário principal da tela).
export function InspecoesCipaTab() {
  const [lista, setLista] = useState<InspecaoCipa[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [membros, setMembros] = useState<MembroCipa[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novo, setNovo] = useState<NovaInspecaoCipa>(vazio());
  const [gerandoNcPara, setGerandoNcPara] = useState<string | null>(null);
  const [responsavelNc, setResponsavelNc] = useState('');
  const [prazoNc, setPrazoNc] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [listaInspecoes, listaObras, listaUsuarios] = await Promise.all([
        api.cipa.inspecoes.listar(),
        api.obras.listar(),
        api.usuarios.listar(),
      ]);
      setLista(listaInspecoes);
      setObras(listaObras);
      setUsuarios(listaUsuarios);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar inspeções da CIPA.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  async function trocarObra(obraId: string) {
    setNovo({ ...novo, obraId, membroCipaId: null });
    setMembros(obraId ? await api.cipa.membros.listar(obraId, true) : []);
  }

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    if (!novo.obraId || !novo.data || !novo.local.trim() || !novo.riscoIdentificado.trim()) {
      setErroPainel('Preencha obra, data, local e o risco identificado.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.cipa.inspecoes.criar(novo);
      setNovo(vazio());
      setMembros([]);
      await carregar();
      sucessoToast('Inspeção registrada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao registrar inspeção.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta inspeção? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cipa.inspecoes.excluir(id);
      await carregar();
      sucessoToast('Inspeção excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir inspeção.');
    }
  }

  async function confirmarGerarNc() {
    if (!gerandoNcPara) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.cipa.inspecoes.gerarNaoConformidade(gerandoNcPara, responsavelNc || null, prazoNc || null);
      setGerandoNcPara(null);
      setResponsavelNc('');
      setPrazoNc('');
      await carregar();
      sucessoToast('Não conformidade gerada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar não conformidade.');
    } finally {
      setCarregando(false);
    }
  }

  const colunas: Coluna<InspecaoCipa>[] = [
    { chave: 'obra', rotulo: 'Obra', render: (i) => nomeObra(i.obraId) },
    { chave: 'data', rotulo: 'Data', render: (i) => i.data?.slice(0, 10) ?? '' },
    { chave: 'local', rotulo: 'Local' },
    { chave: 'riscoIdentificado', rotulo: 'Risco identificado' },
    { chave: 'grauRisco', rotulo: 'Grau', render: (i) => (i.grauRisco != null ? nivelRiscoLabel[i.grauRisco] : '—') },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Inspeções CIPA"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Registrar inspeção
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {gerandoNcPara && (
        <div style={{ marginBottom: 16 }}>
        <Card titulo="Gerar não conformidade">
          <FormGrid>
            <Campo span={6}>
              <Field label="Responsável">
                <Select value={responsavelNc} onChange={(_, d) => setResponsavelNc(d.value)}>
                  <option value="">Nenhum</option>
                  {usuarios.map((usuario) => (
                    <option key={usuario.id} value={usuario.id}>
                      {usuario.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Prazo">
                <CampoData value={prazoNc} onChange={(_, d) => setPrazoNc(d.value)} />
              </Field>
            </Campo>
          </FormGrid>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 8 }}>
            <Button appearance="secondary" onClick={() => setGerandoNcPara(null)}>
              Cancelar
            </Button>
            <Button appearance="primary" onClick={confirmarGerarNc} disabled={carregando}>
              Confirmar
            </Button>
          </div>
        </Card>
        </div>
      )}

      <Card>
        <DataTable
          aria-label="Inspeções registradas"
          colunas={colunas}
          linhas={lista}
          chaveLinha={(i) => i.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma inspeção registrada ainda',
            acao: { rotulo: 'Registrar inspeção', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(i) => (
            <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
              {i.naoConformidadeId ? (
                <StatusChip tom="atencao">NC gerada</StatusChip>
              ) : (
                <Button
                  appearance="subtle"
                  icon={<Warning24Regular />}
                  onClick={() => setGerandoNcPara(i.id)}
                  aria-label="Gerar não conformidade"
                >
                  Gerar NC
                </Button>
              )}
              <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(i.id)} aria-label="Excluir" />
            </div>
          )}
        />
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Nova inspeção"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Registrar inspeção
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}
        <FormGrid>
          <Campo span={3}>
            <Field label="Obra" required>
              <Select value={novo.obraId} onChange={(_, d) => trocarObra(d.value)}>
                <option value="">Selecione</option>
                {obras.map((obra) => (
                  <option key={obra.id} value={obra.id}>
                    {obra.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Membro que inspecionou">
              <Select
                value={novo.membroCipaId ?? ''}
                onChange={(_, d) => setNovo({ ...novo, membroCipaId: d.value || null })}
                disabled={!novo.obraId}
              >
                <option value="">Não informado</option>
                {membros.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.trabalhadorNome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={2}>
            <Field label="Data" required>
              <CampoData value={novo.data} onChange={(_, d) => setNovo({ ...novo, data: d.value })} />
            </Field>
          </Campo>
          <Campo span={4}>
            <Field label="Local" required>
              <Input value={novo.local} onChange={(_, d) => setNovo({ ...novo, local: d.value })} />
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Grau de risco">
              <Select
                value={novo.grauRisco != null ? String(novo.grauRisco) : ''}
                onChange={(_, d) => setNovo({ ...novo, grauRisco: d.value ? Number(d.value) : null })}
              >
                <option value="">Não informado</option>
                {Object.entries(nivelRiscoLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={12}>
            <Field label="Risco identificado" required>
              <Textarea value={novo.riscoIdentificado} onChange={(_, d) => setNovo({ ...novo, riscoIdentificado: d.value })} />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </div>
  );
}
