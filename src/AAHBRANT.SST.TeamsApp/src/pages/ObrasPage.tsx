import { useEffect, useState } from 'react';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, statusObraLabel, StatusObra, type NovaObra, type Obra } from '../lib/api';
import { SeletorFotoCamera } from '../components/SeletorFotoCamera';
import { useSucessoToast } from '../hooks/useSucessoToast';
import {
  Button,
  Campo,
  CampoData,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  PainelLateral,
  Select,
  useConfirmar,
  type Coluna,
} from '@ui';

const obraVazia: NovaObra = {
  codigo: '',
  nome: '',
  status: StatusObra.Planejada,
  dataInicio: '',
  dataPrevisaoTermino: '',
  endereco: '',
  cidade: '',
  uf: '',
  cnpj: '',
};

// Onda 2 Task 21 (camada ui/): mesmo golden rule já aplicado em FuncoesTab.tsx/TrabalhadoresTab.tsx
// (Task 1/2) — o formulário de cadastro, que empurrava a lista pra baixo, sai para um `PainelLateral`
// (conversões 1, 4, 6 do Guia, mais 2 pelo mesmo julgamento de precedente). Erro do painel é estado
// próprio, separado do erro de nível de lista (Guia §4).
export function ObrasPage() {
  const [obras, setObras] = useState<Obra[]>([]);
  const [painelAberto, setPainelAberto] = useState(false);
  const [novaObra, setNovaObra] = useState<NovaObra>(obraVazia);
  const [logoNovaObra, setLogoNovaObra] = useState<File | null>(null);
  const [erroLista, setErroLista] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [logoUrls, setLogoUrls] = useState<Record<string, string>>({});
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErroLista(null);
      setObras(await api.obras.listar());
    } catch (e) {
      setErroLista(e instanceof Error ? e.message : 'Falha ao carregar obras.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  // Miniaturas do logo são baixadas sob demanda (só para obras com temLogo) e mantidas como
  // object URL até a página ser desmontada — diferente do padrão "baixar PDF" já usado no
  // restante do app (que cria e revoga a URL na mesma função), pois aqui a URL precisa
  // permanecer viva para o <img> renderizar.
  useEffect(() => {
    let cancelado = false;
    (async () => {
      for (const obra of obras) {
        if (!obra.temLogo || logoUrls[obra.id]) continue;
        try {
          const blob = await api.obras.baixarLogo(obra.id);
          if (cancelado) return;
          setLogoUrls((atual) => ({ ...atual, [obra.id]: URL.createObjectURL(blob) }));
        } catch {
          // Falha ao carregar miniatura não impede o uso da página; a obra fica sem preview.
        }
      }
    })();
    return () => {
      cancelado = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [obras]);

  useEffect(() => {
    return () => {
      Object.values(logoUrls).forEach((url) => URL.revokeObjectURL(url));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function enviarLogo(obraId: string, arquivo: File) {
    try {
      setErroLista(null);
      await api.obras.anexarLogo(obraId, arquivo);
      setLogoUrls((atual) => {
        const anterior = atual[obraId];
        if (anterior) URL.revokeObjectURL(anterior);
        const { [obraId]: _removido, ...resto } = atual;
        return resto;
      });
      await carregar();
      sucessoToast('Logo atualizado com sucesso.');
    } catch (e) {
      setErroLista(e instanceof Error ? e.message : 'Falha ao enviar o logo.');
    }
  }

  function fecharPainel() {
    setPainelAberto(false);
    setNovaObra(obraVazia);
    setLogoNovaObra(null);
    setErroPainel(null);
  }

  async function criar() {
    if (!logoNovaObra) {
      setErroPainel('A logomarca da obra é obrigatória para finalizar o cadastro.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.obras.criar(
        {
          ...novaObra,
          dataInicio: novaObra.dataInicio || null,
          dataPrevisaoTermino: novaObra.dataPrevisaoTermino || null,
        },
        logoNovaObra,
      );
      await carregar();
      sucessoToast('Obra criada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar obra.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta obra? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.obras.excluir(id);
      await carregar();
      sucessoToast('Obra excluída com sucesso.');
    } catch (e) {
      setErroLista(e instanceof Error ? e.message : 'Falha ao excluir obra.');
    }
  }

  const colunas: Coluna<Obra>[] = [
    { chave: 'codigo', rotulo: 'Código' },
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'cliente', rotulo: 'Cliente' },
    { chave: 'status', rotulo: 'Status', render: (o) => statusObraLabel[o.status] },
    { chave: 'cidadeUf', rotulo: 'Cidade/UF', render: (o) => `${o.cidade ?? ''}${o.uf ? `/${o.uf}` : ''}` },
    { chave: 'cnpj', rotulo: 'CNPJ' },
    {
      chave: 'logo',
      rotulo: 'Logo',
      render: (o) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          {logoUrls[o.id] && (
            <img
              src={logoUrls[o.id]}
              alt={`Logo de ${o.nome}`}
              style={{ height: 32, width: 32, objectFit: 'contain', borderRadius: 4 }}
            />
          )}
          <SeletorFotoCamera
            rotulo="Trocar logo"
            apenasIcone
            aoSelecionarArquivo={(arquivo) => enviarLogo(o.id, arquivo)}
            aoErroValidacao={setErroLista}
          />
        </div>
      ),
    },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Obras cadastradas"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Adicionar obra
          </Button>
        }
      />

      {erroLista && (
        <FeedbackInline tom="erro" aoFechar={() => setErroLista(null)}>
          {erroLista}
        </FeedbackInline>
      )}

      <Card>
        <DataTable
          aria-label="Obras cadastradas"
          colunas={colunas}
          linhas={obras}
          chaveLinha={(o) => o.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma obra cadastrada ainda.',
            acao: { rotulo: 'Adicionar obra', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(o) => (
            <Button
              appearance="subtle"
              icon={<Delete24Regular />}
              onClick={(evento) => {
                evento.stopPropagation();
                excluir(o.id);
              }}
              aria-label="Excluir"
            />
          )}
        />
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Adicionar obra"
        largura="lg"
        rodape={
          <FormRodape info="A logomarca é obrigatória: ela será usada no cabeçalho dos documentos gerados e assinados para esta obra (APR, PT, DDS, Ficha de EPI, Relatório de Fiscalização).">
            <Button appearance="secondary" onClick={fecharPainel}>
              Cancelar
            </Button>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || !logoNovaObra}>
              Adicionar obra
            </Button>
          </FormRodape>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}

        <FormSection titulo="Dados Gerais da Obra" numero={1} primeira>
          <FormGrid>
            <Campo span={2}>
              <Field label="Código">
                <Input value={novaObra.codigo} onChange={(_, d) => setNovaObra({ ...novaObra, codigo: d.value })} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Nome">
                <Input value={novaObra.nome} onChange={(_, d) => setNovaObra({ ...novaObra, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Status">
                <Select
                  value={novaObra.status}
                  onChange={(_, d) => setNovaObra({ ...novaObra, status: Number(d.value) })}
                >
                  {Object.entries(statusObraLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Data de início">
                <CampoData
                  value={novaObra.dataInicio ?? ''}
                  onChange={(_, d) => setNovaObra({ ...novaObra, dataInicio: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Previsão de término">
                <CampoData
                  value={novaObra.dataPrevisaoTermino ?? ''}
                  onChange={(_, d) => setNovaObra({ ...novaObra, dataPrevisaoTermino: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>

        <FormSection titulo="Endereço e Documentos" numero={2}>
          <FormGrid>
            <Campo span={4}>
              <Field label="Endereço">
                <Input
                  value={novaObra.endereco ?? ''}
                  onChange={(_, d) => setNovaObra({ ...novaObra, endereco: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Cidade">
                <Input value={novaObra.cidade ?? ''} onChange={(_, d) => setNovaObra({ ...novaObra, cidade: d.value })} />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="UF">
                <Input
                  value={novaObra.uf ?? ''}
                  maxLength={2}
                  onChange={(_, d) => setNovaObra({ ...novaObra, uf: d.value.toUpperCase() })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="CNPJ">
                <Input
                  value={novaObra.cnpj ?? ''}
                  maxLength={18}
                  onChange={(_, d) => setNovaObra({ ...novaObra, cnpj: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Logomarca da obra" required>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <SeletorFotoCamera
                    rotulo={logoNovaObra ? logoNovaObra.name : 'Tirar foto ou escolher arquivo'}
                    tiposAceitos="image/jpeg,image/png"
                    aoSelecionarArquivo={(arquivo) => setLogoNovaObra(arquivo)}
                    aoErroValidacao={setErroPainel}
                  />
                </div>
              </Field>
            </Campo>
          </FormGrid>
        </FormSection>
      </PainelLateral>
    </div>
  );
}
