import { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Textarea,
} from '@fluentui/react-components';
import { ArrowDownload24Regular, ArrowLeft24Regular, CheckmarkCircle24Regular } from '@fluentui/react-icons';
import { api, type ItemHigienizacaoDetalhe, type Trabalhador } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

export function HigienizacaoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [detalhe, setDetalhe] = useState<ItemHigienizacaoDetalhe | null>(null);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [trabalhadorSelecionado, setTrabalhadorSelecionado] = useState('');
  const [observacoes, setObservacoes] = useState('');
  const [fotoArquivo, setFotoArquivo] = useState<File | null>(null);
  const [fotoPreviewUrl, setFotoPreviewUrl] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoFotoId, setBaixandoFotoId] = useState<string | null>(null);
  const inputFotoRef = useRef<HTMLInputElement>(null);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const det = await api.higienizacao.obterDetalhe(id);
      setDetalhe(det);
      const listaTrabalhadores = await api.trabalhadores.listar(det.item.obraId);
      setTrabalhadores(listaTrabalhadores);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar item de higienização.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function selecionarFoto(arquivo: File | null) {
    setFotoArquivo(arquivo);
    setFotoPreviewUrl((urlAnterior) => {
      if (urlAnterior) URL.revokeObjectURL(urlAnterior);
      return arquivo ? URL.createObjectURL(arquivo) : null;
    });
  }

  async function registrarHigienizacao() {
    if (!id || !trabalhadorSelecionado || !fotoArquivo) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.higienizacao.registrarHigienizacao(id, trabalhadorSelecionado, observacoes || null, fotoArquivo);
      setTrabalhadorSelecionado('');
      setObservacoes('');
      selecionarFoto(null);
      if (inputFotoRef.current) inputFotoRef.current.value = '';
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar higienização.');
    } finally {
      setProcessando(false);
    }
  }

  async function baixarFotoRegistro(registroId: string, dataHora: string) {
    try {
      setBaixandoFotoId(registroId);
      setErro(null);
      const blob = await api.higienizacao.baixarFotoRegistro(registroId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `higienizacao-${dataHora.slice(0, 10)}`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a foto do registro.');
    } finally {
      setBaixandoFotoId(null);
    }
  }

  if (!id) {
    return <Text>Item de higienização não encontrado.</Text>;
  }

  const item = detalhe?.item;

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/prevencao/higienizacao')}
        style={{ marginBottom: 12 }}
      >
        Voltar para Higienização
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {item ? (
          <>
            <Text size={500} weight="semibold">
              {item.nome}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Obra: {item.obraNome}</Text>
              {item.local && <Text>Local: {item.local}</Text>}
              <Text>Periodicidade: a cada {item.periodicidadeDias} dias</Text>
            </div>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, alignItems: 'center' }}>
              <Text>Última higienização: {item.ultimaHigienizacaoEm?.slice(0, 10) ?? 'Nunca'}</Text>
              <Badge appearance="tint">Próximo vencimento: {item.proximoVencimentoEm.slice(0, 10)}</Badge>
            </div>
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Registrar higienização</Text>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <div className={estilos.formActions}>
            <Select
              value={trabalhadorSelecionado}
              onChange={(_, d) => setTrabalhadorSelecionado(d.value)}
              style={{ minWidth: 240 }}
            >
              <option value="">Selecione um responsável</option>
              {trabalhadores.map((trabalhador) => (
                <option key={trabalhador.id} value={trabalhador.id}>
                  {trabalhador.nome} ({trabalhador.matricula})
                </option>
              ))}
            </Select>
          </div>
          <Textarea
            placeholder="Observações (opcional)"
            value={observacoes}
            onChange={(_, d) => setObservacoes(d.value)}
          />
          <div className={estilos.formActions} style={{ alignItems: 'center' }}>
            <input
              ref={inputFotoRef}
              type="file"
              accept="image/*"
              capture="environment"
              onChange={(e) => selecionarFoto(e.target.files?.[0] ?? null)}
            />
            {fotoPreviewUrl && (
              <img
                src={fotoPreviewUrl}
                alt="Pré-visualização da foto"
                style={{ height: 48, width: 48, objectFit: 'cover', borderRadius: 4 }}
              />
            )}
            <Button
              appearance="primary"
              icon={<CheckmarkCircle24Regular />}
              onClick={registrarHigienizacao}
              disabled={processando || !trabalhadorSelecionado || !fotoArquivo}
            >
              Registrar higienização
            </Button>
          </div>
          <Text size={200}>A foto do local higienizado é obrigatória para registrar a higienização.</Text>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Histórico</Text>
        </div>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Data/hora</TableHeaderCell>
              <TableHeaderCell>Responsável</TableHeaderCell>
              <TableHeaderCell>Observações</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {detalhe?.registros.map((registro) => (
              <TableRow key={registro.id}>
                <TableCell>{registro.dataHora.slice(0, 16).replace('T', ' ')}</TableCell>
                <TableCell>{registro.trabalhadorNome}</TableCell>
                <TableCell>{registro.observacoes}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    size="small"
                    icon={<ArrowDownload24Regular />}
                    onClick={() => baixarFotoRegistro(registro.id, registro.dataHora)}
                    disabled={baixandoFotoId === registro.id}
                  >
                    Baixar foto
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
