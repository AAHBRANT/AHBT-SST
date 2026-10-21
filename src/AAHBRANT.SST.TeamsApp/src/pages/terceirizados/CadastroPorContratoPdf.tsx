import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  BotaoAcao,
  Button,
  Campo,
  Checkbox,
  FeedbackInline,
  Field,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
  Text,
} from '@ui';
import { api, type ContratoTerceirizadoLidoIa, type Funcao, type NovaEmpresa, type Obra } from '../../lib/api';
import { DICA_IA, EnvioPdfComIa, dataCampo, sugerirObra, sugerirPorNome } from '../../components/leitura-ia/EnvioPdfComIa';
import { formatarCpf } from '../../lib/cpf';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Cadastro da empresa terceirizada pelo PDF do contrato (18/10/2026) — mesmo modelo do PGR/PCMSO: a IA
// lê o contrato e preenche empresa, contrato, vagas e pessoas; o técnico confere e confirma. Ao
// confirmar, a tela usa as mesmas rotas do cadastro manual, nesta ordem: empresa (se ainda não existe),
// contrato com as vagas e, por fim, cada pessoa na vaga da função dela.

interface VagaForm {
  funcaoId: string;
  quantidade: string;
  funcaoLida: string | null;
}

interface PessoaForm {
  incluir: boolean;
  nome: string;
  cpf: string;
  funcaoId: string;
  funcaoLida: string | null;
  matricula: string;
  dataAdmissao: string;
}

const empresaVazia: NovaEmpresa = {
  razaoSocial: '', nomeFantasia: '', cnpj: '', tipoServicoPrestado: '',
  contatoNome: '', contatoTelefone: '', contatoEmail: '',
};

const pessoaVazia: PessoaForm = {
  incluir: true, nome: '', cpf: '', funcaoId: '', funcaoLida: null, matricula: '', dataAdmissao: '',
};

// Linhas de vaga e de pessoa: campos alinhados pelo topo (a dica embaixo de um campo não desalinha os
// outros) e botões/caixa de seleção descidos até a altura dos campos, já que não têm rótulo.
const linha = { display: 'flex', gap: 12, alignItems: 'flex-start', flexWrap: 'wrap' } as const;
const semRotulo = { marginTop: 28 } as const;

interface Falha {
  nome: string;
  motivo: string;
}

export function CadastroPorContratoPdf({ aoCancelar, aoConcluir }: { aoCancelar: () => void; aoConcluir: () => void }) {
  const navigate = useNavigate();
  const sucessoToast = useSucessoToast();
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [lendo, setLendo] = useState(false);
  const [lido, setLido] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  const [empresa, setEmpresa] = useState<NovaEmpresa>(empresaVazia);
  const [empresaExistente, setEmpresaExistente] = useState<{ id: string; razaoSocial: string } | null>(null);
  const [obraId, setObraId] = useState('');
  const [obraLida, setObraLida] = useState<string | null>(null);
  const [numeroContrato, setNumeroContrato] = useState('');
  const [inicio, setInicio] = useState('');
  const [fim, setFim] = useState('');
  const [vagas, setVagas] = useState<VagaForm[]>([{ funcaoId: '', quantidade: '1', funcaoLida: null }]);
  const [pessoas, setPessoas] = useState<PessoaForm[]>([]);

  // Depois de criar empresa/contrato, uma nova tentativa não pode duplicar o que já foi gravado.
  const [contratoCriadoId, setContratoCriadoId] = useState<string | null>(null);
  const [falhas, setFalhas] = useState<Falha[]>([]);

  // A leitura espera as listas: sem elas não há como sugerir a obra e as funções do que o contrato diz.
  const listas = useRef<Promise<{ obras: Obra[]; funcoes: Funcao[] }> | null>(null);

  useEffect(() => {
    const carregamento = Promise.all([api.obras.listar(), api.funcoes.listar()]).then(([listaObras, listaFuncoes]) => {
      const ordenadas = [...listaFuncoes].sort((a, b) => a.nome.localeCompare(b.nome));
      setObras(listaObras);
      setFuncoes(ordenadas);
      return { obras: listaObras, funcoes: ordenadas };
    });
    carregamento.catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar obras e funções.'));
    listas.current = carregamento;
  }, []);

  function aplicarLeitura(l: ContratoTerceirizadoLidoIa, obras: Obra[], funcoes: Funcao[]) {
    const e = l.empresa;
    setEmpresa({
      razaoSocial: e.razaoSocial ?? '',
      nomeFantasia: e.nomeFantasia ?? '',
      cnpj: (e.cnpj ?? '').slice(0, 14),
      tipoServicoPrestado: e.tipoServicoPrestado ?? '',
      contatoNome: e.contatoNome ?? '',
      contatoTelefone: e.contatoTelefone ?? '',
      contatoEmail: e.contatoEmail ?? '',
    });
    setEmpresaExistente(
      l.empresaExistenteId ? { id: l.empresaExistenteId, razaoSocial: l.empresaExistenteRazaoSocial ?? '' } : null,
    );
    setObraLida(l.obra);
    setObraId(sugerirObra(l.obra, obras) ?? '');
    setNumeroContrato(l.numeroContrato ?? '');
    const dataInicio = dataCampo(l.dataInicioVigencia);
    setInicio(dataInicio);
    setFim(dataCampo(l.dataFimVigencia));

    const pessoasLidas: PessoaForm[] = l.pessoas.map((p) => ({
      incluir: true,
      nome: p.nome,
      cpf: (p.cpf ?? '').slice(0, 11),
      funcaoId: sugerirPorNome(p.funcao, funcoes) ?? '',
      funcaoLida: p.funcao,
      matricula: p.matricula ?? '',
      // Sugestão para conferir: o contrato raramente traz a admissão de cada pessoa.
      dataAdmissao: dataInicio,
    }));
    setPessoas(pessoasLidas);

    let vagasLidas: VagaForm[] = l.vagas.map((v) => ({
      funcaoId: sugerirPorNome(v.funcao, funcoes) ?? '',
      quantidade: v.quantidade ? String(v.quantidade) : '1',
      funcaoLida: v.funcao,
    }));
    // Sem efetivo escrito no contrato, as vagas saem da relação de pessoas (uma vaga por pessoa).
    if (vagasLidas.length === 0 && pessoasLidas.length > 0) {
      const porFuncao = new Map<string, VagaForm>();
      for (const p of pessoasLidas) {
        const chave = p.funcaoId || p.funcaoLida || '';
        const atual = porFuncao.get(chave);
        if (atual) atual.quantidade = String(Number(atual.quantidade) + 1);
        else porFuncao.set(chave, { funcaoId: p.funcaoId, quantidade: '1', funcaoLida: p.funcaoLida });
      }
      vagasLidas = [...porFuncao.values()];
    }
    setVagas(vagasLidas.length > 0 ? vagasLidas : [{ funcaoId: '', quantidade: '1', funcaoLida: null }]);
  }

  async function lerPdf(f: File) {
    setArquivo(f);
    setLendo(true);
    setErro(null);
    try {
      const [leitura, cadastro] = await Promise.all([
        api.terceirizados.contratos.lerComIa(f),
        listas.current ?? Promise.resolve({ obras, funcoes }),
      ]);
      aplicarLeitura(leitura, cadastro.obras, cadastro.funcoes);
      setLido(true);
    } catch (e) {
      setLido(false);
      setErro(e instanceof Error ? e.message : 'A IA não conseguiu ler o contrato. Preencha os campos manualmente.');
    } finally {
      setLendo(false);
    }
  }

  const dica = lido ? DICA_IA : undefined;
  const funcaoNome = (id: string) => funcoes.find((f) => f.id === id)?.nome ?? '';
  const incluidas = pessoas.filter((p) => p.incluir);

  // A vaga de cada função precisa comportar as pessoas marcadas: falta de vaga trava o cadastro da
  // pessoa no backend, então a quantidade sobe sozinha até o número de pessoas daquela função.
  function vagasFinais() {
    const porFuncao = new Map<string, number>();
    for (const v of vagas) porFuncao.set(v.funcaoId, Math.max(porFuncao.get(v.funcaoId) ?? 0, Number(v.quantidade) || 0));
    const pessoasPorFuncao = new Map<string, number>();
    for (const p of incluidas) pessoasPorFuncao.set(p.funcaoId, (pessoasPorFuncao.get(p.funcaoId) ?? 0) + 1);
    for (const [funcaoId, qtd] of pessoasPorFuncao) porFuncao.set(funcaoId, Math.max(porFuncao.get(funcaoId) ?? 0, qtd));
    return [...porFuncao.entries()].map(([funcaoId, quantidade]) => ({ funcaoId, quantidade }));
  }

  function validar(): string | null {
    if (!empresaExistente && (!empresa.razaoSocial.trim() || empresa.cnpj.length !== 14))
      return 'Confira a razão social e o CNPJ (14 números) da empresa.';
    if (!obraId) return 'Escolha a obra do contrato.';
    if (!numeroContrato.trim() || !inicio || !fim) return 'Preencha o número e a vigência do contrato.';
    if (vagas.some((v) => !v.funcaoId))
      return 'Escolha a função cadastrada de cada vaga (ou remova a vaga).';
    const pessoaIncompleta = incluidas.find(
      (p) => !p.nome.trim() || p.cpf.length !== 11 || !p.funcaoId || !p.matricula.trim() || !p.dataAdmissao,
    );
    if (pessoaIncompleta)
      return `Complete nome, CPF, função, matrícula e data de início de ${pessoaIncompleta.nome || 'todas as pessoas marcadas'} — ou desmarque quem não vai entrar agora.`;
    return null;
  }

  async function cadastrar() {
    const problema = validar();
    if (problema) {
      setErro(problema);
      return;
    }
    setSalvando(true);
    setErro(null);
    setFalhas([]);
    try {
      let contratoId = contratoCriadoId;
      if (!contratoId) {
        let empresaId = empresaExistente?.id;
        if (!empresaId) {
          empresaId = (await api.terceirizados.empresas.criar(empresa)).id;
          setEmpresaExistente({ id: empresaId, razaoSocial: empresa.razaoSocial });
        }
        contratoId = (
          await api.terceirizados.contratos.criar({
            empresaId,
            obraId,
            numeroContrato: numeroContrato.trim(),
            dataInicioVigencia: inicio,
            dataFimVigencia: fim,
            vagas: vagasFinais(),
          })
        ).id;
        setContratoCriadoId(contratoId);
      }

      const novasFalhas: Falha[] = [];
      const restantes: PessoaForm[] = [];
      for (const p of pessoas) {
        if (!p.incluir) {
          restantes.push(p);
          continue;
        }
        try {
          await api.terceirizados.contratos.cadastrarPessoaNaVaga(contratoId, p.funcaoId, {
            nome: p.nome.trim(),
            matricula: p.matricula.trim(),
            cpf: p.cpf,
            dataAdmissao: p.dataAdmissao,
          });
        } catch (e) {
          novasFalhas.push({ nome: p.nome, motivo: e instanceof Error ? e.message : 'Falha ao cadastrar.' });
          restantes.push(p);
        }
      }
      aoConcluir();
      if (novasFalhas.length === 0) {
        sucessoToast('Empresa, contrato e pessoas cadastrados. Confira EPI e treinamentos pendentes de cada pessoa.');
        navigate(`/terceirizados/contratos/${contratoId}`);
        return;
      }
      // Fica na tela só quem não entrou, para corrigir e tentar de novo sem recadastrar os outros.
      setPessoas(restantes);
      setFalhas(novasFalhas);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao cadastrar.');
    } finally {
      setSalvando(false);
    }
  }

  function alterarPessoa(indice: number, campos: Partial<PessoaForm>) {
    setPessoas(pessoas.map((p, i) => (i === indice ? { ...p, ...campos } : p)));
  }

  function alterarVaga(indice: number, campos: Partial<VagaForm>) {
    setVagas(vagas.map((v, i) => (i === indice ? { ...v, ...campos } : v)));
  }

  const bloqueadoAposContrato = contratoCriadoId !== null;

  return (
    <>
      <FormSection titulo="Contrato em PDF" numero={1} primeira>
        <EnvioPdfComIa
          arquivo={arquivo}
          lendo={lendo}
          lido={lido}
          aoEscolher={(f) => void lerPdf(f)}
          aoRemover={() => {
            setArquivo(null);
            setLido(false);
          }}
          rotulo="Envie o contrato (PDF) para a IA preencher o cadastro"
          detalheComArquivo="Confira os campos preenchidos antes de cadastrar. O PDF não é guardado."
        />
        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}
        {falhas.length > 0 && contratoCriadoId && (
          <FeedbackInline
            tom="aviso"
            acao={{ rotulo: 'Ir para o contrato', aoClicar: () => navigate(`/terceirizados/contratos/${contratoCriadoId}`) }}
          >
            Empresa e contrato cadastrados, mas {falhas.length} pessoa(s) não entraram:{' '}
            {falhas.map((f) => `${f.nome} (${f.motivo})`).join('; ')}. Corrija abaixo e clique em cadastrar de novo.
          </FeedbackInline>
        )}
      </FormSection>

      <FormSection titulo="Empresa" numero={2}>
        {empresaExistente ? (
          <FeedbackInline tom="info">
            Empresa já cadastrada: <strong>{empresaExistente.razaoSocial}</strong>. O contrato será vinculado a ela.
          </FeedbackInline>
        ) : (
          <FormGrid>
            <Campo span={6}>
              <Field label="Razão social" required hint={dica}>
                <Input value={empresa.razaoSocial} onChange={(_, d) => setEmpresa({ ...empresa, razaoSocial: d.value })} />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Nome fantasia">
                <Input value={empresa.nomeFantasia ?? ''} onChange={(_, d) => setEmpresa({ ...empresa, nomeFantasia: d.value })} />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="CNPJ (só números)" required hint={dica}>
                <Input
                  value={empresa.cnpj}
                  onChange={(_, d) => setEmpresa({ ...empresa, cnpj: d.value.replace(/\D/g, '').slice(0, 14) })}
                />
              </Field>
            </Campo>
            <Campo span={8}>
              <Field label="Serviço prestado">
                <Input
                  value={empresa.tipoServicoPrestado ?? ''}
                  onChange={(_, d) => setEmpresa({ ...empresa, tipoServicoPrestado: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Contato — nome">
                <Input value={empresa.contatoNome ?? ''} onChange={(_, d) => setEmpresa({ ...empresa, contatoNome: d.value })} />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Contato — telefone">
                <Input
                  value={empresa.contatoTelefone ?? ''}
                  onChange={(_, d) => setEmpresa({ ...empresa, contatoTelefone: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Contato — e-mail">
                <Input value={empresa.contatoEmail ?? ''} onChange={(_, d) => setEmpresa({ ...empresa, contatoEmail: d.value })} />
              </Field>
            </Campo>
          </FormGrid>
        )}
      </FormSection>

      <FormSection titulo="Contrato e vagas" numero={3}>
        <FormGrid>
          <Campo span={6}>
            <Field label="Obra" required hint={obraLida ? `No contrato: "${obraLida}" — confira` : undefined}>
              <Select style={{ width: '100%' }} value={obraId} disabled={bloqueadoAposContrato} onChange={(_, d) => setObraId(d.value)}>
                <option value="">Selecione</option>
                {obras.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.codigo} — {o.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Nº do contrato" required hint={dica}>
              <Input
                value={numeroContrato}
                maxLength={50}
                disabled={bloqueadoAposContrato}
                onChange={(_, d) => setNumeroContrato(d.value)}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Início da vigência" required hint={dica}>
              <Input type="date" value={inicio} disabled={bloqueadoAposContrato} onChange={(_, d) => setInicio(d.value)} />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Fim da vigência" required hint={dica}>
              <Input
                type="date"
                value={fim}
                min={inicio || undefined}
                disabled={bloqueadoAposContrato}
                onChange={(_, d) => setFim(d.value)}
              />
            </Field>
          </Campo>
          {vagas.map((vaga, indice) => (
            <Campo span={12} key={indice}>
              <div style={linha}>
                <div style={{ flex: '1 1 260px', minWidth: 0 }}>
                  <Field
                    label="Função da vaga"
                    required
                    hint={vaga.funcaoLida ? `No contrato: "${vaga.funcaoLida}"` : undefined}
                  >
                    <Select style={{ width: '100%' }}
                      value={vaga.funcaoId}
                      disabled={bloqueadoAposContrato}
                      onChange={(_, d) => alterarVaga(indice, { funcaoId: d.value })}
                    >
                      <option value="">Selecione</option>
                      {funcoes.map((f) => (
                        <option key={f.id} value={f.id}>
                          {f.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </div>
                <div style={{ flex: '0 0 140px', minWidth: 0 }}>
                  <Field label="Quantidade" required>
                    <Input
                      type="number"
                      min={1}
                      value={vaga.quantidade}
                      disabled={bloqueadoAposContrato}
                      onChange={(_, d) => alterarVaga(indice, { quantidade: d.value })}
                    />
                  </Field>
                </div>
                <div style={semRotulo}>
                  <BotaoAcao
                    tom="excluir"
                    icon={<Delete24Regular />}
                    disabled={vagas.length === 1 || bloqueadoAposContrato}
                    onClick={() => setVagas(vagas.filter((_, i) => i !== indice))}
                    aria-label="Remover vaga"
                  />
                </div>
              </div>
            </Campo>
          ))}
        </FormGrid>
        {!bloqueadoAposContrato && (
          <div>
            <Button
              appearance="subtle"
              icon={<Add24Regular />}
              onClick={() => setVagas([...vagas, { funcaoId: '', quantidade: '1', funcaoLida: null }])}
            >
              Adicionar função
            </Button>
          </div>
        )}
      </FormSection>

      <FormSection titulo="Pessoas do contrato" numero={4}>
        {pessoas.length === 0 ? (
          <Text size={200}>
            {lido
              ? 'O contrato não traz a relação de pessoas. Cadastre-as depois, em cada vaga do contrato, ou adicione aqui.'
              : 'As pessoas listadas no contrato aparecem aqui depois da leitura.'}
          </Text>
        ) : (
          <FormGrid>
            {pessoas.map((p, indice) => (
              <Campo span={12} key={indice}>
                <div style={linha}>
                  <Checkbox
                    style={semRotulo}
                    checked={p.incluir}
                    onChange={(_, d) => alterarPessoa(indice, { incluir: !!d.checked })}
                    aria-label={`Cadastrar ${p.nome || 'esta pessoa'}`}
                  />
                  <div style={{ flex: '2 1 220px', minWidth: 0 }}>
                    <Field label="Nome" required>
                      <Input value={p.nome} onChange={(_, d) => alterarPessoa(indice, { nome: d.value })} />
                    </Field>
                  </div>
                  <div style={{ flex: '1 1 150px', minWidth: 0 }}>
                    <Field label="CPF" required>
                      <Input
                        value={formatarCpf(p.cpf)}
                        onChange={(_, d) => alterarPessoa(indice, { cpf: d.value.replace(/\D/g, '').slice(0, 11) })}
                      />
                    </Field>
                  </div>
                  <div style={{ flex: '1 1 180px', minWidth: 0 }}>
                    <Field
                      label="Função"
                      required
                      hint={p.funcaoLida && !funcaoNome(p.funcaoId) ? `No contrato: "${p.funcaoLida}"` : undefined}
                    >
                      <Select style={{ width: '100%' }} value={p.funcaoId} onChange={(_, d) => alterarPessoa(indice, { funcaoId: d.value })}>
                        <option value="">Selecione</option>
                        {funcoes.map((f) => (
                          <option key={f.id} value={f.id}>
                            {f.nome}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </div>
                  <div style={{ flex: '1 1 120px', minWidth: 0 }}>
                    <Field label="Matrícula" required>
                      <Input value={p.matricula} onChange={(_, d) => alterarPessoa(indice, { matricula: d.value })} />
                    </Field>
                  </div>
                  <div style={{ flex: '1 1 150px', minWidth: 0 }}>
                    <Field label="Início na obra" required hint={lido ? 'Confira' : undefined}>
                      <Input
                        type="date"
                        value={p.dataAdmissao}
                        onChange={(_, d) => alterarPessoa(indice, { dataAdmissao: d.value })}
                      />
                    </Field>
                  </div>
                  <div style={semRotulo}>
                    <BotaoAcao
                      tom="excluir"
                      icon={<Delete24Regular />}
                      onClick={() => setPessoas(pessoas.filter((_, i) => i !== indice))}
                      aria-label="Remover pessoa"
                    />
                  </div>
                </div>
              </Campo>
            ))}
          </FormGrid>
        )}
        <div>
          <Button
            appearance="subtle"
            icon={<Add24Regular />}
            onClick={() => setPessoas([...pessoas, { ...pessoaVazia, dataAdmissao: inicio }])}
          >
            Adicionar pessoa
          </Button>
        </div>
        <FormRodape info="EPI e treinamentos obrigatórios da função são verificados automaticamente para cada pessoa.">
          <Button onClick={aoCancelar}>Cancelar</Button>
          <Button appearance="primary" onClick={() => void cadastrar()} disabled={salvando || lendo}>
            {bloqueadoAposContrato
              ? `Cadastrar ${incluidas.length} pessoa(s) restante(s)`
              : `Cadastrar empresa, contrato${incluidas.length > 0 ? ` e ${incluidas.length} pessoa(s)` : ''}`}
          </Button>
        </FormRodape>
      </FormSection>
    </>
  );
}
