import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { AddCircle24Regular, ChevronRight24Regular } from '@fluentui/react-icons';
import {
  api,
  statusDocumentoGestaoLabel,
  type DocumentoGestao,
  type NovoDocumentoGestao,
  type Obra,
  type RequisitoLegal,
  type Setor,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function novoInicial(): NovoDocumentoGestao {
  return {
    nome: '',
    tipo: '',
    categoria: '',
    origemDocumento: '',
    responsavelUsuarioId: '',
    versao: '',
    validade: '',
    dataEmissao: new Date().toISOString().slice(0, 10),
    requisitoLegalId: '',
    obraId: '',
    setorId: '',
    arquivo: '',
  };
}

export function DocumentosGestaoPage() {
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [documentos, setDocumentos] = useState<DocumentoGestao[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [setores, setSetores] = useState<Setor[]>([]);
  const [requisitosLegais, setRequisitosLegais] = useState<RequisitoLegal[]>([]);
  const [novo, setNovo] = useState<NovoDocumentoGestao>(novoInicial());
  const [filtroNome, setFiltroNome] = useState('');
  const [filtroTipo, setFiltroTipo] = useState('');
  const [filtroCategoria, setFiltroCategoria] = useState('');
  const [filtroStatus, setFiltroStatus] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaUsuarios, listaObras, listaSetores, listaRequisitos] = await Promise.all([
        api.gestaoDocumental.listar({
          nome: filtroNome || undefined,
          tipo: filtroTipo || undefined,
          categoria: filtroCategoria || undefined,
          status: filtroStatus ? Number(filtroStatus) : undefined,
        }),
        api.usuarios.listar(),
        api.obras.listar(),
        api.setores.listar(),
        api.matrizLegal.listar(),
      ]);
      setDocumentos(lista);
      setUsuarios(listaUsuarios);
      setObras(listaObras);
      setSetores(listaSetores);
      setRequisitosLegais(listaRequisitos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar a gestão documental.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtroNome, filtroTipo, filtroCategoria, filtroStatus]);

  async function criar() {
    if (!novo.nome.trim()) {
      setErro('Informe o nome do documento.');
      return;
    }
    if (!novo.dataEmissao) {
      setErro('Informe a data de emissão.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.gestaoDocumental.criar({
        ...novo,
        tipo: novo.tipo || null,
        categoria: novo.categoria || null,
        origemDocumento: novo.origemDocumento || null,
        responsavelUsuarioId: novo.responsavelUsuarioId || null,
        versao: novo.versao || null,
        validade: novo.validade || null,
        requisitoLegalId: novo.requisitoLegalId || null,
        obraId: novo.obraId || null,
        setorId: novo.setorId || null,
        arquivo: novo.arquivo || null,
      });
      setNovo(novoInicial());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar documento.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Novo documento</Text>
        </div>
        <div className={estilos.form}>
          <Field label="Nome" required>
            <Input value={novo.nome} onChange={(_, d) => setNovo({ ...novo, nome: d.value })} />
          </Field>
          <Field label="Tipo">
            <Input value={novo.tipo ?? ''} onChange={(_, d) => setNovo({ ...novo, tipo: d.value })} />
          </Field>
          <Field label="Categoria">
            <Input value={novo.categoria ?? ''} onChange={(_, d) => setNovo({ ...novo, categoria: d.value })} />
          </Field>
          <Field label="Origem">
            <Input
              value={novo.origemDocumento ?? ''}
              onChange={(_, d) => setNovo({ ...novo, origemDocumento: d.value })}
            />
          </Field>
          <Field label="Versão">
            <Input value={novo.versao ?? ''} onChange={(_, d) => setNovo({ ...novo, versao: d.value })} />
          </Field>
          <Field label="Data de emissão" required>
            <Input
              type="date"
              value={novo.dataEmissao}
              onChange={(_, d) => setNovo({ ...novo, dataEmissao: d.value })}
            />
          </Field>
          <Field label="Validade">
            <Input
              type="date"
              value={novo.validade ?? ''}
              onChange={(_, d) => setNovo({ ...novo, validade: d.value })}
            />
          </Field>
          <Field label="Obra">
            <Select value={novo.obraId ?? ''} onChange={(_, d) => setNovo({ ...novo, obraId: d.value })}>
              <option value="">Global (todas as obras)</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Setor">
            <Select value={novo.setorId ?? ''} onChange={(_, d) => setNovo({ ...novo, setorId: d.value })}>
              <option value="">Nenhum</option>
              {setores.map((setor) => (
                <option key={setor.id} value={setor.id}>
                  {setor.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Requisito legal relacionado">
            <Select
              value={novo.requisitoLegalId ?? ''}
              onChange={(_, d) => setNovo({ ...novo, requisitoLegalId: d.value })}
            >
              <option value="">Nenhum</option>
              {requisitosLegais.map((requisito) => (
                <option key={requisito.id} value={requisito.id}>
                  {requisito.codigo} — {requisito.tema}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Responsável">
            <Select
              value={novo.responsavelUsuarioId ?? ''}
              onChange={(_, d) => setNovo({ ...novo, responsavelUsuarioId: d.value })}
            >
              <option value="">Nenhum</option>
              {usuarios.map((usuario) => (
                <option key={usuario.id} value={usuario.id}>
                  {usuario.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Arquivo">
            <Input
              value={novo.arquivo ?? ''}
              placeholder="Referência/link do arquivo"
              onChange={(_, d) => setNovo({ ...novo, arquivo: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={carregando}>
            Adicionar documento
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Gestão documental</Text>
          <Field label="Nome">
            <Input value={filtroNome} onChange={(_, d) => setFiltroNome(d.value)} />
          </Field>
          <Field label="Tipo">
            <Input value={filtroTipo} onChange={(_, d) => setFiltroTipo(d.value)} />
          </Field>
          <Field label="Categoria">
            <Input value={filtroCategoria} onChange={(_, d) => setFiltroCategoria(d.value)} />
          </Field>
          <Field label="Status">
            <Select value={filtroStatus} onChange={(_, d) => setFiltroStatus(d.value)}>
              <option value="">Todos</option>
              {Object.entries(statusDocumentoGestaoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>Tipo</TableHeaderCell>
              <TableHeaderCell>Categoria</TableHeaderCell>
              <TableHeaderCell>Versão</TableHeaderCell>
              <TableHeaderCell>Responsável</TableHeaderCell>
              <TableHeaderCell>Validade</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {documentos.map((documento) => (
              <TableRow
                key={documento.id}
                onClick={() => navigate(`/conformidade/gestao-documental/${documento.id}`)}
                style={{ cursor: 'pointer' }}
              >
                <TableCell>{documento.nome}</TableCell>
                <TableCell>{documento.tipo ?? '—'}</TableCell>
                <TableCell>{documento.categoria ?? '—'}</TableCell>
                <TableCell>{documento.versao ?? '—'}</TableCell>
                <TableCell>{documento.responsavelUsuarioNome ?? '—'}</TableCell>
                <TableCell>{documento.validade?.slice(0, 10) ?? '—'}</TableCell>
                <TableCell>
                  <Badge appearance="tint">{statusDocumentoGestaoLabel[documento.status]}</Badge>
                </TableCell>
                <TableCell>
                  <ChevronRight24Regular />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
