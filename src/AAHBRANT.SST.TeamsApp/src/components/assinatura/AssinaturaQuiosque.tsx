import { useEffect, useState } from 'react';
import {
  Badge,
  Button,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  tokens,
} from '@fluentui/react-components';
import { Checkmark24Filled, Fingerprint24Regular, Warning24Regular } from '@fluentui/react-icons';
import { api, metodoAutenticacaoAssinaturaLabel, type DocumentoAssinatura } from '../../lib/api';
import { capturarDigitalLocal, estaAgenteLocalDisponivel, obterDispositivoLocal } from '../../lib/agenteBiometricoLocal';
import { SeletorFotoCamera } from '../SeletorFotoCamera';
import { MutacaoEnfileiradaOfflineError } from '../../lib/offline/syncEngine';
import { usePageStyles } from '../../pages/pageStyles';

function extrairMensagemErro(e: unknown, fallback: string): string {
  if (!(e instanceof Error)) return fallback;
  const trechoJson = e.message.match(/\{.*\}$/);
  if (trechoJson) {
    try {
      const corpo = JSON.parse(trechoJson[0]) as { erro?: string };
      if (typeof corpo.erro === 'string') return corpo.erro;
    } catch {
      // corpo não era JSON — cai no fallback abaixo
    }
  }
  return e.message || fallback;
}

export interface AssinaturaQuiosqueProps {
  entidadeTipo: string;
  entidadeId: string;
  // Obra do trabalhador que vai assinar — necessária só pelo bloco de reconhecimento facial (o
  // Identify busca no PersonGroup da obra). Os demais métodos (Futronic/sessão logada) não usam.
  obraId: string;
}

// Bloco de quiosque do Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5,
// etapa 14 — extraído de AssinarDdsPage.tsx para permitir reuso em Treinamento/EPI/APR/PT/Inspeções
// sem duplicar a lógica de autenticação). O backend já é genérico desde a etapa 6
// (EntidadeTipo/EntidadeId); esta extração torna o frontend igualmente plugável — para usar em um
// novo módulo, basta renderizar <AssinaturaQuiosque entidadeTipo="..." entidadeId={id} /> dentro da
// página de detalhe do módulo. Crachá/QR+PIN e WebAuthn/FIDO2 foram removidos do sistema em 31/08
// (decisão do usuário: único método de assinatura é a digital via leitor Futronic FS80H, "para não
// dar conflitos" com métodos alternativos) — sem o leitor local disponível não há como assinar aqui.
export function AssinaturaQuiosque({ entidadeTipo, entidadeId, obraId }: AssinaturaQuiosqueProps) {
  const estilos = usePageStyles();
  const [documento, setDocumento] = useState<DocumentoAssinatura | null>(null);
  const [processando, setProcessando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [ultimoAssinante, setUltimoAssinante] = useState<string | null>(null);
  const [agenteLocalDisponivel, setAgenteLocalDisponivel] = useState(false);
  const [dispositivoLocal, setDispositivoLocal] = useState<{ dispositivoId: string; segredoDispositivo: string } | null>(null);
  const [pendenteFacial, setPendenteFacial] = useState(false);

  useEffect(() => {
    estaAgenteLocalDisponivel().then(async (disponivel) => {
      setAgenteLocalDisponivel(disponivel);
      if (disponivel) {
        const dispositivo = await obterDispositivoLocal();
        setDispositivoLocal(dispositivo);
      }
    });
  }, []);

  async function carregar() {
    try {
      setErro(null);
      await api.assinatura.criar(entidadeTipo, entidadeId);
      const doc = await api.assinatura.obter(entidadeTipo, entidadeId);
      setDocumento(doc);
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha ao carregar a tela de assinatura.'));
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entidadeTipo, entidadeId]);

  async function assinarComBiometriaLocal() {
    if (!documento || !dispositivoLocal) return;
    try {
      setProcessando(true);
      setErro(null);
      setUltimoAssinante(null);
      const captura = await capturarDigitalLocal();
      const signatario = await api.assinatura.autenticarBiometriaLocal(
        documento.id,
        dispositivoLocal.dispositivoId,
        dispositivoLocal.segredoDispositivo,
        captura.trabalhadorId,
        captura.score,
      );
      setUltimoAssinante(signatario.trabalhadorNome);
      const doc = await api.assinatura.obter(entidadeTipo, entidadeId);
      setDocumento(doc);
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha na autenticação via biometria local.'));
    } finally {
      setProcessando(false);
    }
  }

  // Pedido do usuário (01/09): Patrulha de Segurança/Inspeção aceita só 1 assinatura (o backend já
  // rejeita uma segunda em IRegistradorAssinaturaService — isso aqui é só a UI não deixar tentar de
  // novo). Outros documentos (DDS, PT, EPI) continuam com quantas assinaturas forem necessárias.
  const assinaturaUnicaConcluida = entidadeTipo === 'Inspecao' && (documento?.signatarios.length ?? 0) > 0;

  async function assinarComFacial(arquivo: File) {
    if (!documento) return;
    try {
      setErro(null);
      setPendenteFacial(false);
      setUltimoAssinante(null);
      const signatario = await api.assinatura.autenticarFacial(documento.id, obraId, arquivo);
      setUltimoAssinante(signatario.trabalhadorNome);
      const doc = await api.assinatura.obter(entidadeTipo, entidadeId);
      setDocumento(doc);
    } catch (e) {
      if (e instanceof MutacaoEnfileiradaOfflineError) {
        setPendenteFacial(true);
        return;
      }
      setErro(extrairMensagemErro(e, 'Falha na autenticação facial.'));
    }
  }

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      {assinaturaUnicaConcluida ? (
        <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
            <Text weight="semibold">Inspeção assinada</Text>
          </div>
          <Text style={{ display: 'block', marginTop: 8 }}>
            A Patrulha de Segurança aceita apenas uma assinatura, já registrada abaixo.
          </Text>
        </div>
      ) : agenteLocalDisponivel && dispositivoLocal ? (
        <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
          <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
            Digital (leitor local — Futronic FS80H)
          </Text>
          <Button
            appearance="primary"
            size="large"
            icon={<Fingerprint24Regular />}
            onClick={assinarComBiometriaLocal}
            disabled={processando || !documento}
          >
            Autenticar com digital
          </Button>
          {ultimoAssinante && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 12 }}>
              <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
              <Text>Assinatura registrada: {ultimoAssinante}</Text>
            </div>
          )}
        </div>
      ) : (
        <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Warning24Regular />
            <Text weight="semibold">Leitor Futronic não encontrado nesta máquina</Text>
          </div>
          <Text style={{ display: 'block', marginTop: 8 }}>
            Verifique se o leitor está conectado e se o Agente Biométrico está em execução, depois
            recarregue esta página — ou use o reconhecimento facial abaixo.
          </Text>
        </div>
      )}

      {!assinaturaUnicaConcluida && (
        <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
          <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
            Reconhecimento Facial (Azure)
          </Text>
          {pendenteFacial && (
            <Text style={{ display: 'block', marginBottom: 8 }}>
              Sem internet — a foto foi salva neste dispositivo e será verificada assim que a conexão voltar.
            </Text>
          )}
          <SeletorFotoCamera aoSelecionarArquivo={assinarComFacial} rotulo="Assinar com reconhecimento facial" desabilitado={!documento} />
        </div>
      )}

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Assinaturas registradas ({documento?.signatarios.length ?? 0})</Text>
        </div>
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>Método</TableHeaderCell>
              <TableHeaderCell>Horário</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {documento?.signatarios.map((signatario) => (
              <TableRow key={signatario.trabalhadorId}>
                <TableCell>{signatario.trabalhadorNome}</TableCell>
                <TableCell>
                  <Badge appearance="tint">{metodoAutenticacaoAssinaturaLabel[signatario.metodoAutenticacao]}</Badge>
                </TableCell>
                <TableCell>{new Date(signatario.assinadoEm).toLocaleTimeString('pt-BR')}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
