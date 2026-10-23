import { useEffect, useRef, useState, type DragEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { FolderOpen24Regular, DocumentMultiple24Regular } from '@fluentui/react-icons';
import {
  BarraProgresso,
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
  StatusChip,
  Text,
  designTokens,
  tokensUi,
} from '@ui';
import {
  api,
  OrigemCertificadoTreinamento,
  resultadoAsoLabel,
  type ArquivoTerceirizadoClassificado,
  type Contrato,
  type ContratoTerceirizadoLidoIa,
  type CursoTreinamento,
  type DocumentoExigidoEmpresa,
  type Empresa,
  type Funcao,
  type NovaEmpresa,
  type Obra,
  type TipoArquivoTerceirizado,
} from '../../lib/api';
import { DICA_IA, dataCampo, sugerirObra, sugerirPorNome } from '../../components/leitura-ia/EnvioPdfComIa';
import { formatarCpf } from '../../lib/cpf';
import { useSucessoToast } from '../../hooks/useSucessoToast';

// Importação da pasta da terceirizada (23/10/2026, desenho aprovado pelo usuário). A pasta chega
// misturada: contrato, documentos da empresa (PGR, PCMSO, cartão CNPJ...) e ASO, certificados e fichas de
// EPI de cada funcionário. Cada arquivo vai para a IA, que diz o que ele é e de quem é; o contrato ainda
// passa pela leitura completa (a mesma do "Cadastrar pelo contrato"). Nada é gravado até o técnico
// conferir e clicar em "Cadastrar tudo" — aí a tela usa as mesmas rotas do cadastro manual: empresa,
// contrato, documentos da empresa, cada pessoa na vaga da função e, para cada pessoa, ASO e certificados
// com o arquivo anexado. Ficha de EPI enviada pela empresa ainda não tem onde ser guardada no SST.

// Estilos inline com os tokens do @ui (páginas não importam @fluentui/react-components direto).
const estilos = {
  area: {
    display: 'flex',
    alignItems: 'center',
    gap: 14,
    flexWrap: 'wrap',
    padding: 16,
    borderRadius: tokensUi.raio.lg,
    border: `2px dashed ${designTokens.colorPrimary}`,
    backgroundColor: designTokens.colorPageBackground,
  },
  arrastando: { backgroundColor: designTokens.colorInfoWash },
  icone: { color: designTokens.colorPrimary, flexShrink: 0 },
  texto: { display: 'flex', flexDirection: 'column', minWidth: 0, flex: 1, gap: 4 },
  bloco: {
    border: `1px solid ${designTokens.colorCardBorder}`,
    borderRadius: tokensUi.raio.md,
    padding: 12,
    display: 'flex',
    flexDirection: 'column',
    gap: 10,
  },
  cabecalhoBloco: { display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' },
  arquivo: {
    borderTop: `1px solid ${tokensUi.bordaSuave}`,
    paddingTop: 10,
    display: 'flex',
    flexDirection: 'column',
    gap: 6,
  },
  nomeArquivo: { fontWeight: 600, wordBreak: 'break-all' },
} as const;

const linha = { display: 'flex', gap: 12, alignItems: 'flex-start', flexWrap: 'wrap' } as const;
const coluna = (base: number) => ({ flex: `1 1 ${base}px`, minWidth: 0 }) as const;

const EXTENSOES = /\.(pdf|jpe?g|png)$/i;
const PARALELO = 3;

const rotuloTipo: Record<TipoArquivoTerceirizado, string> = {
  contrato: 'Contrato',
  documento_empresa: 'Documento da empresa',
  aso: 'ASO',
  certificado: 'Certificado de treinamento',
  ficha_epi: 'Ficha de EPI',
  outro: 'Ignorar',
};

const tiposPessoa: TipoArquivoTerceirizado[] = ['aso', 'certificado', 'ficha_epi'];

const tipoExameLabel: Record<number, string> = {
  1: 'Admissional',
  2: 'Periódico',
  3: 'Retorno ao trabalho',
  4: 'Mudança de função',
  5: 'Demissional',
};

interface ItemArquivo {
  id: number;
  arquivo: File;
  situacao: 'fila' | 'lendo' | 'lido' | 'erro';
  erro: string | null;
  resumo: string | null;
  tipo: TipoArquivoTerceirizado;
  documentoExigidoId: string;
  cursoId: string;
  pessoaChave: string;
  dataDocumento: string;
  dataValidade: string;
  asoTipo: string;
  asoResultado: string;
  medicoNome: string;
  medicoCrm: string;
  cargaHoraria: string;
  instituicao: string;
  // Lido pela IA, guardado para montar as pessoas.
  pessoaNome: string | null;
  pessoaCpf: string | null;
  pessoaFuncao: string | null;
  empresaCnpj: string | null;
  empresaRazaoSocial: string | null;
  gravado: boolean;
}

interface PessoaImport {
  chave: string;
  incluir: boolean;
  nome: string;
  cpf: string;
  funcaoId: string;
  funcaoLida: string | null;
  matricula: string;
  dataAdmissao: string;
  // Preenchido quando a pessoa já existe na empresa ou depois de cadastrada nesta importação.
  trabalhadorId: string | null;
  existente: boolean;
}

interface Falha {
  oque: string;
  motivo: string;
}

const empresaVazia: NovaEmpresa = {
  razaoSocial: '', nomeFantasia: '', cnpj: '', tipoServicoPrestado: '',
  contatoNome: '', contatoTelefone: '', contatoEmail: '',
};

const somenteDigitos = (valor: string | null | undefined) => (valor ?? '').replace(/\D/g, '');

function normalizarNome(nome: string | null | undefined) {
  return (nome ?? '')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/\s+/g, ' ')
    .trim();
}

function mensagem(e: unknown, padrao: string) {
  return e instanceof Error ? e.message : padrao;
}

function somarMeses(dataIso: string, meses: number) {
  const d = new Date(`${dataIso}T00:00:00Z`);
  d.setUTCMonth(d.getUTCMonth() + meses);
  return d.toISOString().slice(0, 10);
}

function itemDe(id: number, arquivo: File): ItemArquivo {
  return {
    id, arquivo, situacao: 'fila', erro: null, resumo: null, tipo: 'outro',
    documentoExigidoId: '', cursoId: '', pessoaChave: '', dataDocumento: '', dataValidade: '',
    asoTipo: '', asoResultado: '', medicoNome: '', medicoCrm: '', cargaHoraria: '', instituicao: '',
    pessoaNome: null, pessoaCpf: null, pessoaFuncao: null, empresaCnpj: null, empresaRazaoSocial: null,
    gravado: false,
  };
}

function aplicarClassificacao(item: ItemArquivo, c: ArquivoTerceirizadoClassificado): ItemArquivo {
  return {
    ...item,
    situacao: 'lido',
    resumo: c.resumo,
    tipo: c.tipo,
    documentoExigidoId: c.documentoExigidoId ?? '',
    cursoId: c.cursoTreinamentoId ?? '',
    dataDocumento: dataCampo(c.dataDocumento),
    dataValidade: dataCampo(c.dataValidade),
    asoTipo: c.asoTipo ? String(c.asoTipo) : '',
    asoResultado: c.asoResultado ? String(c.asoResultado) : '',
    medicoNome: c.medicoNome ?? '',
    medicoCrm: c.medicoCrm ?? '',
    cargaHoraria: c.cargaHoraria ? String(c.cargaHoraria) : '',
    instituicao: c.instituicao ?? '',
    pessoaNome: c.pessoaNome,
    pessoaCpf: c.pessoaCpf,
    pessoaFuncao: c.pessoaFuncao,
    empresaCnpj: c.empresaCnpj,
    empresaRazaoSocial: c.empresaRazaoSocial,
  };
}

// Pasta arrastada: o navegador entrega diretórios como entradas a percorrer.
type Entrada = {
  isFile: boolean;
  isDirectory: boolean;
  file?: (ok: (f: File) => void, erro: (e: unknown) => void) => void;
  createReader?: () => { readEntries: (ok: (e: Entrada[]) => void, erro: (e: unknown) => void) => void };
};

async function arquivosDaEntrada(entrada: Entrada): Promise<File[]> {
  if (entrada.isFile && entrada.file) {
    return [await new Promise<File>((ok, erro) => entrada.file!(ok, erro))];
  }
  if (entrada.isDirectory && entrada.createReader) {
    const leitor = entrada.createReader();
    const todas: Entrada[] = [];
    // readEntries devolve em lotes; vazio = acabou.
    for (;;) {
      const lote = await new Promise<Entrada[]>((ok, erro) => leitor.readEntries(ok, erro));
      if (lote.length === 0) break;
      todas.push(...lote);
    }
    return (await Promise.all(todas.map(arquivosDaEntrada))).flat();
  }
  return [];
}

export function ImportarPastaTerceirizado({ aoCancelar, aoConcluir }: { aoCancelar: () => void; aoConcluir: () => void }) {
  const navigate = useNavigate();
  const sucessoToast = useSucessoToast();
  const entradaPasta = useRef<HTMLInputElement>(null);
  const entradaArquivos = useRef<HTMLInputElement>(null);

  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [exigidos, setExigidos] = useState<DocumentoExigidoEmpresa[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [empresas, setEmpresas] = useState<Empresa[]>([]);

  const [etapa, setEtapa] = useState<'escolher' | 'lendo' | 'conferir'>('escolher');
  const [arrastando, setArrastando] = useState(false);
  const [itens, setItens] = useState<ItemArquivo[]>([]);
  const [ignoradosNaEntrada, setIgnoradosNaEntrada] = useState(0);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [falhas, setFalhas] = useState<Falha[]>([]);

  const [leituraContrato, setLeituraContrato] = useState<ContratoTerceirizadoLidoIa | null>(null);
  const [empresa, setEmpresa] = useState<NovaEmpresa>(empresaVazia);
  const [empresaExistente, setEmpresaExistente] = useState<{ id: string; razaoSocial: string } | null>(null);
  const [contratosEmpresa, setContratosEmpresa] = useState<Contrato[]>([]);
  // '' = criar um contrato novo; senão, id do contrato existente da empresa.
  const [contratoEscolhido, setContratoEscolhido] = useState('');
  const [obraId, setObraId] = useState('');
  const [obraLida, setObraLida] = useState<string | null>(null);
  const [numeroContrato, setNumeroContrato] = useState('');
  const [inicio, setInicio] = useState('');
  const [fim, setFim] = useState('');
  const [pessoas, setPessoas] = useState<PessoaImport[]>([]);

  // Depois de gravar empresa/contrato, uma nova tentativa não pode duplicar o que já foi gravado.
  const [contratoCriadoId, setContratoCriadoId] = useState<string | null>(null);

  useEffect(() => {
    Promise.all([
      api.obras.listar(),
      api.funcoes.listar(),
      api.terceirizados.documentosExigidos.listar(),
      api.cursosTreinamento.listar(),
      api.terceirizados.empresas.listar(),
    ])
      .then(([o, f, d, c, e]) => {
        setObras(o);
        setFuncoes([...f].sort((a, b) => a.nome.localeCompare(b.nome)));
        setExigidos(d.filter((x) => x.ativo));
        setCursos([...c].sort((a, b) => a.nome.localeCompare(b.nome)));
        setEmpresas(e);
      })
      .catch((e) => setErro(mensagem(e, 'Falha ao carregar as listas do cadastro.')));
  }, []);

  function alterarItem(id: number, campos: Partial<ItemArquivo>) {
    setItens((atual) => atual.map((i) => (i.id === id ? { ...i, ...campos } : i)));
  }

  function alterarPessoa(chave: string, campos: Partial<PessoaImport>) {
    setPessoas((atual) => atual.map((p) => (p.chave === chave ? { ...p, ...campos } : p)));
  }

  async function iniciar(arquivos: File[]) {
    const aceitos = arquivos.filter((f) => EXTENSOES.test(f.name));
    setIgnoradosNaEntrada(arquivos.length - aceitos.length);
    if (aceitos.length === 0) {
      setErro('Nenhum PDF, JPG ou PNG na pasta escolhida.');
      return;
    }
    setErro(null);
    const lista = aceitos.map((f, i) => itemDe(i + 1, f));
    setItens(lista);
    setEtapa('lendo');

    const resultado = [...lista];
    let proximo = 0;
    let contrato: ContratoTerceirizadoLidoIa | null = null;
    const trabalhar = async () => {
      while (proximo < resultado.length) {
        const indice = proximo++;
        const item = resultado[indice];
        alterarItem(item.id, { situacao: 'lendo' });
        try {
          const c = await api.terceirizados.importacao.classificar(item.arquivo);
          resultado[indice] = aplicarClassificacao(item, c);
          // O primeiro contrato da pasta passa pela leitura completa (empresa, vigência, vagas, pessoas).
          if (c.tipo === 'contrato' && !contrato && item.arquivo.name.toLowerCase().endsWith('.pdf')) {
            contrato = await api.terceirizados.contratos.lerComIa(item.arquivo).catch(() => null);
          }
        } catch (e) {
          resultado[indice] = { ...item, situacao: 'erro', erro: mensagem(e, 'A IA não conseguiu ler este arquivo.') };
        }
        const pronto = resultado[indice];
        setItens((atual) => atual.map((i) => (i.id === pronto.id ? pronto : i)));
      }
    };
    await Promise.all(Array.from({ length: Math.min(PARALELO, resultado.length) }, trabalhar));
    await montarConferencia(resultado, contrato);
    setEtapa('conferir');
  }

  // Junta o que veio do contrato e dos documentos: empresa, contrato e uma pessoa por CPF (ou nome).
  async function montarConferencia(lidos: ItemArquivo[], contrato: ContratoTerceirizadoLidoIa | null) {
    setLeituraContrato(contrato);
    const doc = lidos.find((i) => i.empresaCnpj?.length === 14);
    const e = contrato?.empresa;
    const cnpj = somenteDigitos(e?.cnpj) || doc?.empresaCnpj || '';
    setEmpresa({
      ...empresaVazia,
      razaoSocial: e?.razaoSocial ?? doc?.empresaRazaoSocial ?? '',
      nomeFantasia: e?.nomeFantasia ?? '',
      cnpj: cnpj.slice(0, 14),
      tipoServicoPrestado: e?.tipoServicoPrestado ?? '',
      contatoNome: e?.contatoNome ?? '',
      contatoTelefone: e?.contatoTelefone ?? '',
      contatoEmail: e?.contatoEmail ?? '',
    });
    const existente = contrato?.empresaExistenteId
      ? { id: contrato.empresaExistenteId, razaoSocial: contrato.empresaExistenteRazaoSocial ?? '' }
      : (() => {
          const achada = cnpj.length === 14 ? empresas.find((x) => somenteDigitos(x.cnpj) === cnpj) : undefined;
          return achada ? { id: achada.id, razaoSocial: achada.razaoSocial } : null;
        })();
    setEmpresaExistente(existente);

    if (contrato) {
      setObraLida(contrato.obra);
      setObraId(sugerirObra(contrato.obra, obras) ?? '');
      setNumeroContrato(contrato.numeroContrato ?? '');
      setInicio(dataCampo(contrato.dataInicioVigencia));
      setFim(dataCampo(contrato.dataFimVigencia));
    }
    const dataInicio = dataCampo(contrato?.dataInicioVigencia ?? null) || new Date().toISOString().slice(0, 10);

    let pessoasEmpresa: { trabalhadorId: string; nome: string; funcaoId: string }[] = [];
    let contratosExistentes: Contrato[] = [];
    if (existente) {
      [pessoasEmpresa, contratosExistentes] = await Promise.all([
        api.terceirizados.pessoas.listar({ empresaId: existente.id }).catch(() => []),
        api.terceirizados.contratos.listarPorEmpresa(existente.id).catch(() => [] as Contrato[]),
      ]);
    }
    const validados = contratosExistentes.filter((c) => c.status === 'Validado');
    setContratosEmpresa(validados);
    // Sem contrato na pasta e empresa já cadastrada: as pessoas entram no contrato vigente dela.
    setContratoEscolhido(!contrato && validados.length > 0 ? validados[0].id : '');

    const lista: PessoaImport[] = [];
    let sequencia = 0;
    const adicionar = (nome: string | null, cpf: string | null, funcao: string | null, matricula: string | null) => {
      const cpfLimpo = somenteDigitos(cpf).slice(0, 11);
      const nomeNormalizado = normalizarNome(nome);
      let p = lista.find(
        (x) => (cpfLimpo.length === 11 && x.cpf === cpfLimpo) || (nomeNormalizado && normalizarNome(x.nome) === nomeNormalizado),
      );
      if (!p) {
        const jaCadastrada = pessoasEmpresa.find((x) => normalizarNome(x.nome) === nomeNormalizado);
        p = {
          chave: `p${++sequencia}`,
          incluir: true,
          nome: nome?.trim() ?? '',
          cpf: cpfLimpo,
          funcaoId: jaCadastrada?.funcaoId ?? sugerirPorNome(funcao, funcoes) ?? '',
          funcaoLida: funcao,
          matricula: matricula ?? '',
          dataAdmissao: dataInicio,
          trabalhadorId: jaCadastrada?.trabalhadorId ?? null,
          existente: !!jaCadastrada,
        };
        lista.push(p);
      } else {
        if (!p.cpf && cpfLimpo.length === 11) p.cpf = cpfLimpo;
        if (!p.funcaoId && funcao) {
          p.funcaoId = sugerirPorNome(funcao, funcoes) ?? '';
          p.funcaoLida = funcao;
        }
        if (!p.matricula && matricula) p.matricula = matricula;
      }
      return p;
    };
    for (const p of contrato?.pessoas ?? []) adicionar(p.nome, p.cpf, p.funcao, p.matricula);
    const comPessoa = lidos.map((i) =>
      tiposPessoa.includes(i.tipo) && i.pessoaNome
        ? { ...i, pessoaChave: adicionar(i.pessoaNome, i.pessoaCpf, i.pessoaFuncao, null).chave }
        : i,
    );
    setPessoas(lista);
    setItens(comPessoa);
  }

  function escolherArquivos(lista: FileList | null) {
    if (lista && lista.length > 0) void iniciar(Array.from(lista));
  }

  async function soltar(e: DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setArrastando(false);
    const entradas = Array.from(e.dataTransfer.items)
      .map((i) => (i as unknown as { webkitGetAsEntry?: () => Entrada | null }).webkitGetAsEntry?.())
      .filter((x): x is Entrada => !!x);
    const arquivos = entradas.length > 0 ? (await Promise.all(entradas.map(arquivosDaEntrada))).flat() : Array.from(e.dataTransfer.files);
    if (arquivos.length > 0) void iniciar(arquivos);
  }

  const usandoContratoExistente = contratoEscolhido !== '';
  const incluidas = pessoas.filter((p) => p.incluir);
  const novas = incluidas.filter((p) => !p.existente);
  const funcaoNome = (id: string) => funcoes.find((f) => f.id === id)?.nome ?? '';
  const cursoDe = (id: string) => cursos.find((c) => c.id === id);
  const pessoaDe = (chave: string) => pessoas.find((p) => p.chave === chave);

  const docsEmpresa = itens.filter((i) => i.situacao === 'lido' && i.tipo === 'documento_empresa');
  const docsPessoa = (chave: string) => itens.filter((i) => tiposPessoa.includes(i.tipo) && i.pessoaChave === chave);
  const semDestino = itens.filter(
    (i) =>
      i.situacao === 'erro' ||
      i.tipo === 'outro' ||
      (tiposPessoa.includes(i.tipo) && !pessoaDe(i.pessoaChave)),
  );
  const contratos = itens.filter((i) => i.tipo === 'contrato');

  // Arquivos que serão gravados: documento da empresa e ASO/certificado de pessoa marcada.
  const aGravar = itens.filter(
    (i) =>
      !i.gravado &&
      i.situacao === 'lido' &&
      (i.tipo === 'documento_empresa' || ((i.tipo === 'aso' || i.tipo === 'certificado') && pessoaDe(i.pessoaChave)?.incluir)),
  );

  function vagasNovoContrato() {
    const porFuncao = new Map<string, number>();
    for (const v of leituraContrato?.vagas ?? []) {
      const id = sugerirPorNome(v.funcao, funcoes);
      if (id) porFuncao.set(id, Math.max(porFuncao.get(id) ?? 0, v.quantidade ?? 1));
    }
    const pessoasPorFuncao = new Map<string, number>();
    for (const p of novas) pessoasPorFuncao.set(p.funcaoId, (pessoasPorFuncao.get(p.funcaoId) ?? 0) + 1);
    for (const [id, qtd] of pessoasPorFuncao) porFuncao.set(id, Math.max(porFuncao.get(id) ?? 0, qtd));
    return [...porFuncao.entries()].filter(([id]) => id).map(([funcaoId, quantidade]) => ({ funcaoId, quantidade }));
  }

  function validar(): string | null {
    if (!empresaExistente && (!empresa.razaoSocial.trim() || empresa.cnpj.length !== 14))
      return 'Confira a razão social e o CNPJ (14 números) da empresa.';
    if (!usandoContratoExistente && !contratoCriadoId) {
      if (!obraId) return 'Escolha a obra do contrato.';
      if (!numeroContrato.trim() || !inicio || !fim) return 'Preencha o número e a vigência do contrato.';
      if (vagasNovoContrato().length === 0)
        return 'O contrato novo precisa de ao menos uma vaga: marque uma pessoa com função ou use um contrato existente.';
    }
    const incompleta = novas.find((p) => !p.nome.trim() || p.cpf.length !== 11 || !p.funcaoId || !p.matricula.trim() || !p.dataAdmissao);
    if (incompleta)
      return `Complete nome, CPF, função, matrícula e início de ${incompleta.nome || 'todas as pessoas marcadas'} — ou desmarque quem não vai entrar agora.`;
    for (const i of aGravar) {
      if (i.tipo === 'documento_empresa' && !i.documentoExigidoId)
        return `Escolha o documento exigido de "${i.arquivo.name}" (ou marque como Ignorar).`;
      if (i.tipo === 'aso' && (!i.asoTipo || !i.asoResultado || !i.dataDocumento || !i.dataValidade))
        return `Complete tipo, resultado, data do exame e validade do ASO "${i.arquivo.name}".`;
      if (i.tipo === 'certificado' && (!i.cursoId || !i.dataDocumento))
        return `Escolha o curso e a data de realização do certificado "${i.arquivo.name}".`;
    }
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
    const novasFalhas: Falha[] = [];
    try {
      let empresaId = empresaExistente?.id;
      if (!empresaId) {
        empresaId = (await api.terceirizados.empresas.criar(empresa)).id;
        setEmpresaExistente({ id: empresaId, razaoSocial: empresa.razaoSocial });
      }

      let contratoId = usandoContratoExistente ? contratoEscolhido : contratoCriadoId;
      if (!contratoId) {
        contratoId = (
          await api.terceirizados.contratos.criar({
            empresaId,
            obraId,
            numeroContrato: numeroContrato.trim(),
            dataInicioVigencia: inicio,
            dataFimVigencia: fim,
            vagas: vagasNovoContrato(),
          })
        ).id;
        setContratoCriadoId(contratoId);
        const pdfContrato = contratos.find((i) => i.arquivo.name.toLowerCase().endsWith('.pdf'));
        if (pdfContrato) {
          try {
            await api.terceirizados.contratos.anexarArquivo(contratoId, pdfContrato.arquivo);
            alterarItem(pdfContrato.id, { gravado: true });
          } catch (e) {
            novasFalhas.push({ oque: `PDF do contrato (${pdfContrato.arquivo.name})`, motivo: mensagem(e, 'Falha ao guardar.') });
          }
        }
      }

      for (const i of aGravar.filter((x) => x.tipo === 'documento_empresa')) {
        try {
          await api.terceirizados.empresas.enviarDocumento(empresaId, i.documentoExigidoId, i.dataValidade || null, i.arquivo);
          alterarItem(i.id, { gravado: true });
        } catch (e) {
          novasFalhas.push({ oque: i.arquivo.name, motivo: mensagem(e, 'Falha ao enviar.') });
        }
      }

      const trabalhadorPorChave = new Map<string, string>();
      for (const p of incluidas) {
        if (p.trabalhadorId) {
          trabalhadorPorChave.set(p.chave, p.trabalhadorId);
          continue;
        }
        try {
          const { trabalhadorId } = await api.terceirizados.contratos.cadastrarPessoaNaVaga(contratoId, p.funcaoId, {
            nome: p.nome.trim(),
            matricula: p.matricula.trim(),
            cpf: p.cpf,
            dataAdmissao: p.dataAdmissao,
          });
          trabalhadorPorChave.set(p.chave, trabalhadorId);
          alterarPessoa(p.chave, { trabalhadorId });
        } catch (e) {
          novasFalhas.push({ oque: p.nome, motivo: mensagem(e, 'Falha ao cadastrar.') });
        }
      }

      for (const i of aGravar.filter((x) => x.tipo === 'aso' || x.tipo === 'certificado')) {
        const trabalhadorId = trabalhadorPorChave.get(i.pessoaChave);
        if (!trabalhadorId) continue; // a pessoa não entrou: o erro dela já está na lista
        try {
          if (i.tipo === 'aso') {
            const { id } = await api.asos.criar({
              trabalhadorId,
              tipo: Number(i.asoTipo),
              dataExame: i.dataDocumento,
              dataValidade: i.dataValidade,
              resultadoStatus: Number(i.asoResultado),
              medicoNome: i.medicoNome || null,
              medicoCrm: i.medicoCrm || null,
            });
            await api.asos.anexarArquivo(id, i.arquivo);
          } else {
            const curso = cursoDe(i.cursoId);
            const validade =
              i.dataValidade || (curso && curso.validadeEmMeses > 0 ? somarMeses(i.dataDocumento, curso.validadeEmMeses) : i.dataDocumento);
            const { id } = await api.treinamentos.criar({
              trabalhadorId,
              cursoTreinamentoId: i.cursoId,
              dataRealizacao: i.dataDocumento,
              dataValidade: validade,
              cargaHorariaRealizada: Number(i.cargaHoraria) || curso?.cargaHorariaMinima || 0,
              instituicaoInstrutor: i.instituicao || null,
              origemCertificado: OrigemCertificadoTreinamento.Externo,
            });
            await api.treinamentos.anexarArquivoCertificado(id, i.arquivo);
          }
          alterarItem(i.id, { gravado: true });
        } catch (e) {
          novasFalhas.push({ oque: `${rotuloTipo[i.tipo]} — ${i.arquivo.name}`, motivo: mensagem(e, 'Falha ao gravar.') });
        }
      }

      aoConcluir();
      setFalhas(novasFalhas);
      if (novasFalhas.length === 0) {
        sucessoToast('Pasta importada. Confira EPI e treinamentos pendentes de cada pessoa.');
        navigate(`/terceirizados/contratos/${contratoId}`);
      }
    } catch (e) {
      setErro(mensagem(e, 'Falha ao cadastrar.'));
    } finally {
      setSalvando(false);
    }
  }

  // ---------- Telas ----------

  const lidos = itens.filter((i) => i.situacao === 'lido' || i.situacao === 'erro').length;

  if (etapa !== 'conferir') {
    return (
      <FormSection titulo="Pasta da empresa" numero={1} primeira>
        <div
          style={{ ...estilos.area, ...(arrastando ? estilos.arrastando : {}) }}
          onDragOver={(e) => {
            e.preventDefault();
            setArrastando(true);
          }}
          onDragLeave={() => setArrastando(false)}
          onDrop={(e) => void soltar(e)}
        >
          <FolderOpen24Regular style={estilos.icone} />
          <div style={estilos.texto}>
            {etapa === 'lendo' ? (
              <>
                <Text weight="semibold">
                  A IA está lendo os arquivos: {lidos} de {itens.length}
                </Text>
                <BarraProgresso percentual={itens.length ? Math.round((lidos / itens.length) * 100) : 0} />
                <Text size={200}>Pode levar alguns minutos numa pasta grande. Não feche esta tela.</Text>
              </>
            ) : (
              <>
                <Text weight="semibold">Arraste aqui a pasta da empresa terceirizada</Text>
                <Text size={200}>
                  PDF, JPG e PNG, tudo misturado: contrato, documentos da empresa, ASO, certificados e fichas de cada
                  funcionário. Nada é gravado antes de você conferir.
                </Text>
              </>
            )}
          </div>
          {etapa === 'escolher' && (
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <Button icon={<FolderOpen24Regular />} onClick={() => entradaPasta.current?.click()}>
                Escolher pasta
              </Button>
              <Button icon={<DocumentMultiple24Regular />} onClick={() => entradaArquivos.current?.click()}>
                Escolher arquivos
              </Button>
            </div>
          )}
          <input
            ref={entradaPasta}
            type="file"
            hidden
            multiple
            {...({ webkitdirectory: '', directory: '' } as Record<string, string>)}
            onChange={(e) => escolherArquivos(e.target.files)}
          />
          <input
            ref={entradaArquivos}
            type="file"
            hidden
            multiple
            accept=".pdf,.jpg,.jpeg,.png"
            onChange={(e) => escolherArquivos(e.target.files)}
          />
        </div>
        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}
        <FormRodape>
          <Button onClick={aoCancelar}>Cancelar</Button>
        </FormRodape>
      </FormSection>
    );
  }

  function seletorTipo(i: ItemArquivo) {
    return (
      <div style={coluna(200)}>
        <Field label="O que é">
          <Select style={{ width: '100%' }} value={i.tipo} onChange={(_, d) => alterarItem(i.id, { tipo: d.value as TipoArquivoTerceirizado })}>
            {(Object.keys(rotuloTipo) as TipoArquivoTerceirizado[]).map((t) => (
              <option key={t} value={t}>
                {rotuloTipo[t]}
              </option>
            ))}
          </Select>
        </Field>
      </div>
    );
  }

  function seletorPessoa(i: ItemArquivo) {
    return (
      <div style={coluna(220)}>
        <Field label="De quem" hint={i.pessoaNome && !pessoaDe(i.pessoaChave) ? `No documento: "${i.pessoaNome}"` : undefined}>
          <Select style={{ width: '100%' }} value={i.pessoaChave} onChange={(_, d) => alterarItem(i.id, { pessoaChave: d.value })}>
            <option value="">Selecione</option>
            {pessoas.map((p) => (
              <option key={p.chave} value={p.chave}>
                {p.nome || '(sem nome)'}
              </option>
            ))}
          </Select>
        </Field>
      </div>
    );
  }

  function camposArquivo(i: ItemArquivo, comPessoa: boolean) {
    return (
      <div style={estilos.arquivo} key={i.id}>
        <div style={estilos.cabecalhoBloco}>
          <Text style={estilos.nomeArquivo}>{i.arquivo.name}</Text>
          {i.gravado && <StatusChip tom="ok">Gravado</StatusChip>}
          {i.situacao === 'erro' && <StatusChip tom="alerta">Não lido</StatusChip>}
        </div>
        {(i.resumo || i.erro) && <Text size={200}>{i.erro ?? i.resumo}</Text>}
        <div style={linha}>
          {seletorTipo(i)}
          {comPessoa && tiposPessoa.includes(i.tipo) && seletorPessoa(i)}
          {i.tipo === 'documento_empresa' && (
            <>
              <div style={coluna(240)}>
                <Field label="Documento exigido" required>
                  <Select
                    style={{ width: '100%' }}
                    value={i.documentoExigidoId}
                    onChange={(_, d) => alterarItem(i.id, { documentoExigidoId: d.value })}
                  >
                    <option value="">Selecione</option>
                    {exigidos.map((d) => (
                      <option key={d.id} value={d.id}>
                        {d.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <div style={coluna(150)}>
                <Field label="Validade" hint={i.dataValidade ? DICA_IA : undefined}>
                  <Input type="date" value={i.dataValidade} onChange={(_, d) => alterarItem(i.id, { dataValidade: d.value })} />
                </Field>
              </div>
            </>
          )}
          {i.tipo === 'aso' && (
            <>
              <div style={coluna(160)}>
                <Field label="Tipo do exame" required>
                  <Select style={{ width: '100%' }} value={i.asoTipo} onChange={(_, d) => alterarItem(i.id, { asoTipo: d.value })}>
                    <option value="">Selecione</option>
                    {Object.entries(tipoExameLabel).map(([v, r]) => (
                      <option key={v} value={v}>
                        {r}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <div style={coluna(160)}>
                <Field label="Resultado" required>
                  <Select
                    style={{ width: '100%' }}
                    value={i.asoResultado}
                    onChange={(_, d) => alterarItem(i.id, { asoResultado: d.value })}
                  >
                    <option value="">Selecione</option>
                    {[1, 2, 3].map((v) => (
                      <option key={v} value={String(v)}>
                        {resultadoAsoLabel[v]}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <div style={coluna(150)}>
                <Field label="Data do exame" required>
                  <Input type="date" value={i.dataDocumento} onChange={(_, d) => alterarItem(i.id, { dataDocumento: d.value })} />
                </Field>
              </div>
              <div style={coluna(150)}>
                <Field label="Validade" required hint={i.dataValidade ? undefined : 'Não veio no documento'}>
                  <Input type="date" value={i.dataValidade} onChange={(_, d) => alterarItem(i.id, { dataValidade: d.value })} />
                </Field>
              </div>
            </>
          )}
          {i.tipo === 'certificado' && (
            <>
              <div style={coluna(240)}>
                <Field label="Curso" required>
                  <Select style={{ width: '100%' }} value={i.cursoId} onChange={(_, d) => alterarItem(i.id, { cursoId: d.value })}>
                    <option value="">Selecione</option>
                    {cursos.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <div style={coluna(150)}>
                <Field label="Realizado em" required>
                  <Input type="date" value={i.dataDocumento} onChange={(_, d) => alterarItem(i.id, { dataDocumento: d.value })} />
                </Field>
              </div>
              <div style={coluna(150)}>
                <Field label="Validade" hint={i.dataValidade ? undefined : 'Vazio: calculada pela validade do curso'}>
                  <Input type="date" value={i.dataValidade} onChange={(_, d) => alterarItem(i.id, { dataValidade: d.value })} />
                </Field>
              </div>
            </>
          )}
          {i.tipo === 'ficha_epi' && (
            <Text size={200} style={{ alignSelf: 'center' }}>
              Ficha de EPI da empresa ainda não é guardada no SST — será ignorada.
            </Text>
          )}
          {i.tipo === 'contrato' && (
            <Text size={200} style={{ alignSelf: 'center' }}>
              Usado para preencher o contrato; o PDF fica guardado nele.
            </Text>
          )}
        </div>
      </div>
    );
  }

  return (
    <>
      <FormSection titulo="Pasta da empresa" numero={1} primeira>
        <FeedbackInline tom="info">
          A IA leu {itens.filter((i) => i.situacao === 'lido').length} de {itens.length} arquivo(s)
          {ignoradosNaEntrada > 0 ? ` (${ignoradosNaEntrada} de outro formato ficaram de fora)` : ''}. Confira tudo abaixo antes de
          cadastrar.
        </FeedbackInline>
        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}
        {falhas.length > 0 && (
          <FeedbackInline tom="aviso" aoFechar={() => setFalhas([])}>
            O que deu certo já foi gravado, mas {falhas.length} item(ns) não entraram:{' '}
            {falhas.map((f) => `${f.oque} (${f.motivo})`).join('; ')}. Corrija e clique em cadastrar de novo — o que já foi gravado
            não é repetido.
          </FeedbackInline>
        )}
      </FormSection>

      <FormSection titulo="Empresa e contrato" numero={2}>
        {empresaExistente ? (
          <FeedbackInline tom="info">
            Empresa já cadastrada: <strong>{empresaExistente.razaoSocial}</strong>.
          </FeedbackInline>
        ) : (
          <FormGrid>
            <Campo span={6}>
              <Field label="Razão social" required hint={empresa.razaoSocial ? DICA_IA : undefined}>
                <Input value={empresa.razaoSocial} onChange={(_, d) => setEmpresa({ ...empresa, razaoSocial: d.value })} />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="CNPJ (só números)" required hint={empresa.cnpj ? DICA_IA : undefined}>
                <Input
                  value={empresa.cnpj}
                  onChange={(_, d) => setEmpresa({ ...empresa, cnpj: d.value.replace(/\D/g, '').slice(0, 14) })}
                />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Nome fantasia">
                <Input value={empresa.nomeFantasia ?? ''} onChange={(_, d) => setEmpresa({ ...empresa, nomeFantasia: d.value })} />
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Serviço prestado">
                <Input
                  value={empresa.tipoServicoPrestado ?? ''}
                  onChange={(_, d) => setEmpresa({ ...empresa, tipoServicoPrestado: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
        )}
        <FormGrid>
          {contratosEmpresa.length > 0 && !contratoCriadoId && (
            <Campo span={12}>
              <Field label="Contrato">
                <Select style={{ width: '100%' }} value={contratoEscolhido} onChange={(_, d) => setContratoEscolhido(d.value)}>
                  <option value="">Novo contrato{contratos.length > 0 ? ' (do PDF da pasta)' : ''}</option>
                  {contratosEmpresa.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.numeroContrato} — {c.obraNome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
          )}
          {!usandoContratoExistente && (
            <>
              <Campo span={6}>
                <Field label="Obra" required hint={obraLida ? `No contrato: "${obraLida}" — confira` : undefined}>
                  <Select style={{ width: '100%' }} value={obraId} disabled={!!contratoCriadoId} onChange={(_, d) => setObraId(d.value)}>
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
                <Field label="Nº do contrato" required>
                  <Input
                    value={numeroContrato}
                    maxLength={50}
                    disabled={!!contratoCriadoId}
                    onChange={(_, d) => setNumeroContrato(d.value)}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Início da vigência" required>
                  <Input type="date" value={inicio} disabled={!!contratoCriadoId} onChange={(_, d) => setInicio(d.value)} />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Fim da vigência" required>
                  <Input
                    type="date"
                    value={fim}
                    min={inicio || undefined}
                    disabled={!!contratoCriadoId}
                    onChange={(_, d) => setFim(d.value)}
                  />
                </Field>
              </Campo>
            </>
          )}
        </FormGrid>
        {!usandoContratoExistente && !contratoCriadoId && (
          <Text size={200}>
            As vagas do contrato novo saem do contrato lido e das pessoas marcadas abaixo (uma vaga por pessoa, por função).
          </Text>
        )}
        {contratos.map((i) => camposArquivo(i, false))}
      </FormSection>

      <FormSection titulo={`Documentos da empresa (${docsEmpresa.length})`} numero={3}>
        {docsEmpresa.length === 0 ? (
          <Text size={200}>Nenhum documento da empresa identificado na pasta.</Text>
        ) : (
          docsEmpresa.map((i) => camposArquivo(i, false))
        )}
      </FormSection>

      <FormSection titulo={`Pessoas (${pessoas.length})`} numero={4}>
        {pessoas.length === 0 && <Text size={200}>Nenhum funcionário identificado nos documentos da pasta.</Text>}
        {pessoas.map((p) => {
          const docs = docsPessoa(p.chave);
          return (
            <div style={estilos.bloco} key={p.chave}>
              <div style={estilos.cabecalhoBloco}>
                <Checkbox
                  checked={p.incluir}
                  onChange={(_, d) => alterarPessoa(p.chave, { incluir: !!d.checked })}
                  aria-label={`Incluir ${p.nome || 'esta pessoa'}`}
                />
                <Text weight="semibold">{p.nome || '(sem nome)'}</Text>
                {p.existente ? (
                  <StatusChip tom="atencao">Já cadastrada — só os documentos entram</StatusChip>
                ) : p.trabalhadorId ? (
                  <StatusChip tom="ok">Cadastrada</StatusChip>
                ) : (
                  <StatusChip tom="info">Nova</StatusChip>
                )}
                <Text size={200}>{docs.length} documento(s)</Text>
              </div>
              {!p.existente && !p.trabalhadorId && p.incluir && (
                <div style={linha}>
                  <div style={coluna(220)}>
                    <Field label="Nome" required>
                      <Input value={p.nome} onChange={(_, d) => alterarPessoa(p.chave, { nome: d.value })} />
                    </Field>
                  </div>
                  <div style={coluna(150)}>
                    <Field label="CPF" required>
                      <Input
                        value={formatarCpf(p.cpf)}
                        onChange={(_, d) => alterarPessoa(p.chave, { cpf: d.value.replace(/\D/g, '').slice(0, 11) })}
                      />
                    </Field>
                  </div>
                  <div style={coluna(180)}>
                    <Field
                      label="Função"
                      required
                      hint={p.funcaoLida && !funcaoNome(p.funcaoId) ? `No documento: "${p.funcaoLida}"` : undefined}
                    >
                      <Select style={{ width: '100%' }} value={p.funcaoId} onChange={(_, d) => alterarPessoa(p.chave, { funcaoId: d.value })}>
                        <option value="">Selecione</option>
                        {funcoes.map((f) => (
                          <option key={f.id} value={f.id}>
                            {f.nome}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </div>
                  <div style={coluna(120)}>
                    <Field label="Matrícula" required>
                      <Input value={p.matricula} onChange={(_, d) => alterarPessoa(p.chave, { matricula: d.value })} />
                    </Field>
                  </div>
                  <div style={coluna(150)}>
                    <Field label="Início na obra" required hint="Confira">
                      <Input type="date" value={p.dataAdmissao} onChange={(_, d) => alterarPessoa(p.chave, { dataAdmissao: d.value })} />
                    </Field>
                  </div>
                </div>
              )}
              {p.incluir && docs.map((i) => camposArquivo(i, true))}
            </div>
          );
        })}
      </FormSection>

      {semDestino.length > 0 && (
        <FormSection titulo={`Não identificados ou ignorados (${semDestino.length})`} numero={5}>
          <Text size={200}>
            Escolha o que é cada arquivo e de quem é. O que ficar como "Ignorar" não é gravado.
          </Text>
          {semDestino.map((i) => camposArquivo(i, true))}
        </FormSection>
      )}

      <FormSection titulo="Cadastrar" numero={semDestino.length > 0 ? 6 : 5}>
        <FormRodape info="EPI e treinamentos obrigatórios da função são verificados automaticamente para cada pessoa.">
          <Button onClick={aoCancelar}>Cancelar</Button>
          <Button appearance="primary" onClick={() => void cadastrar()} disabled={salvando}>
            {salvando
              ? 'Cadastrando…'
              : `Cadastrar tudo (${novas.filter((p) => !p.trabalhadorId).length} pessoa(s), ${aGravar.length} documento(s))`}
          </Button>
        </FormRodape>
      </FormSection>
    </>
  );
}
