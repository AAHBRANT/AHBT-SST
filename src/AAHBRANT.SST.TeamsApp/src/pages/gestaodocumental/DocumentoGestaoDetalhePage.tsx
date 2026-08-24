import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
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
import { ArrowLeft24Regular, Save24Regular } from '@fluentui/react-icons';
import {
  api,
  statusDocumentoGestaoLabel,
  type DocumentoGestaoDetalhe,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

export function DocumentoGestaoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [detalhe, setDetalhe] = useState<DocumentoGestaoDetalhe | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novoStatus, setNovoStatus] = useState<string>('');
  const [motivoRevisao, setMotivoRevisao] = useState('');
  const [novaVersaoRevisao, setNovaVersaoRevisao] = useState('');
  const [responsavelRevisao, setResponsavelRevisao] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, listaUsuarios] = await Promise.all([
        api.gestaoDocumental.obterDetalhe(id),
        api.usuarios.listar(),
      ]);
      setDetalhe(det);
      setUsuarios(listaUsuarios);
      setNovoStatus(String(det.documento.status));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar documento.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function reclassificarStatus() {
    if (!id || !novoStatus) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.gestaoDocumental.atualizarStatus(id, Number(novoStatus));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao reclassificar status.');
    } finally {
      setProcessando(false);
    }
  }

  async function registrarRevisao() {
    if (!id) return;
    if (!motivoRevisao.trim()) {
      setErro('Informe o motivo da revisão.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.gestaoDocumental.criarRevisao(
        id,
        motivoRevisao,
        responsavelRevisao || null,
        novaVersaoRevisao || null,
      );
      setMotivoRevisao('');
      setNovaVersaoRevisao('');
      setResponsavelRevisao('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar revisão.');
    } finally {
      setProcessando(false);
    }
  }

  if (!id) {
    return <Text>Documento não encontrado.</Text>;
  }

  const d = detalhe?.documento;

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/conformidade/gestao-documental')}
        style={{ marginBottom: 12 }}
      >
        Voltar para Gestão Documental
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {d ? (
          <>
            <Text size={500} weight="semibold">
              {d.nome}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              {d.tipo && <Text>Tipo: {d.tipo}</Text>}
              {d.categoria && <Text>Categoria: {d.categoria}</Text>}
              {d.versao && <Text>Versão: {d.versao}</Text>}
              <Badge appearance="tint">{statusDocumentoGestaoLabel[d.status]}</Badge>
            </div>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              {d.origemDocumento && <Text>Origem: {d.origemDocumento}</Text>}
              {d.obraNome && <Text>Obra: {d.obraNome}</Text>}
              {!d.obraNome && <Text>Obra: Global (todas as obras)</Text>}
              {d.setorNome && <Text>Setor: {d.setorNome}</Text>}
              {d.responsavelUsuarioNome && <Text>Responsável: {d.responsavelUsuarioNome}</Text>}
            </div>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Data de emissão: {d.dataEmissao.slice(0, 10)}</Text>
              {d.dataRevisao && <Text>Data de revisão: {d.dataRevisao.slice(0, 10)}</Text>}
              {d.validade && <Text>Validade: {d.validade.slice(0, 10)}</Text>}
              {d.requisitoLegalCodigo && <Text>Requisito relacionado: {d.requisitoLegalCodigo}</Text>}
              {d.arquivo && <Text>Arquivo: {d.arquivo}</Text>}
            </div>

            <div className={estilos.formActions} style={{ marginTop: 16, alignItems: 'flex-end' }}>
              <Field label="Reclassificar status">
                <Select value={novoStatus} onChange={(_, e) => setNovoStatus(e.value)}>
                  {Object.entries(statusDocumentoGestaoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
              <Button
                appearance="primary"
                icon={<Save24Regular />}
                onClick={reclassificarStatus}
                disabled={processando || Number(novoStatus) === d.status}
              >
                Salvar status
              </Button>
            </div>
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Registrar revisão (histórico)</Text>
        </div>
        <div className={estilos.form}>
          <Field label="Motivo" required>
            <Input value={motivoRevisao} onChange={(_, e) => setMotivoRevisao(e.value)} />
          </Field>
          <Field label="Nova versão (opcional)">
            <Input
              value={novaVersaoRevisao}
              placeholder="Deixe em branco para manter a versão atual"
              onChange={(_, e) => setNovaVersaoRevisao(e.value)}
            />
          </Field>
          <Field label="Responsável">
            <Select value={responsavelRevisao} onChange={(_, e) => setResponsavelRevisao(e.value)}>
              <option value="">Nenhum</option>
              {usuarios.map((usuario) => (
                <option key={usuario.id} value={usuario.id}>
                  {usuario.nome}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" onClick={registrarRevisao} disabled={processando}>
            Registrar revisão
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Histórico de revisões</Text>
        </div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nº</TableHeaderCell>
              <TableHeaderCell>Data</TableHeaderCell>
              <TableHeaderCell>Motivo</TableHeaderCell>
              <TableHeaderCell>Responsável</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {detalhe?.historico.map((revisao) => (
              <TableRow key={revisao.id}>
                <TableCell>{revisao.numeroRevisao}</TableCell>
                <TableCell>{revisao.dataRevisao.slice(0, 10)}</TableCell>
                <TableCell>{revisao.motivo}</TableCell>
                <TableCell>{revisao.responsavelUsuarioNome ?? '—'}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
