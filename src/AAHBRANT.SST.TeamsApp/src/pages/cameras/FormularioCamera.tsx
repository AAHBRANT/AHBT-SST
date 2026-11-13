import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Campo, FeedbackInline, Field, FormGrid, FormRodape, FormSection, Input, Select, Switch, tokensUi } from '@ui';
import { api, FonteCamera, ParteImagemCamera, StreamCamera, TipoCamera, tipoCameraLabel, type CameraResumo, type DadosCamera, type Obra } from '../../lib/api';
import { useAgentesCamera, useCameras, useEquipamentosCamera } from '../../lib/consultas';
import { OrigemImagemCamera, type OrigemImagem } from './OrigemImagemCamera';
import { ehCameraIp } from './rotulos';
import { useEstilosCameraIp } from './estilosCameraIp';

const INTERVALOS_PADRAO = [0, 5, 10, 30, 60];
// Câmera nova nasce com 30 minutos entre alertas de quem não foi identificado (igual ao padrão do servidor).
const INTERVALO_INICIAL = 30;

function rotuloIntervalo(minutos: number): string {
  return minutos === 0 ? 'Sem limite — alerta a cada ocorrência' : `${minutos} minutos`;
}

export interface FormularioCameraProps {
  camera?: CameraResumo;
  obras: Obra[];
  obraInicial?: string;
  // Cadastro aberto a partir de uma lente livre da aba "Equipamentos e agentes": já nasce como câmera IP daquele equipamento.
  ipInicial?: { equipamentoId: string; canal: number };
  aoSalvar: (obraId: string) => void;
  aoCancelar: () => void;
}

export function FormularioCamera({ camera, obras, obraInicial, ipInicial, aoSalvar, aoCancelar }: FormularioCameraProps) {
  const edicao = !!camera;
  const [obraId, setObraId] = useState(camera?.obraId ?? obraInicial ?? '');
  const [nome, setNome] = useState(camera?.nome ?? '');
  const [areaSstId, setAreaSstId] = useState(camera?.areaSstId ?? '');
  const [tipo, setTipo] = useState<number>(camera?.tipo ?? TipoCamera.Canteiro);
  const ip = useEstilosCameraIp();
  const [habilitada, setHabilitada] = useState(camera?.habilitada ?? true);
  // Câmera sem os campos (resposta antiga) vale imagem inteira e com aviso no Telegram.
  const [avisarTelegram, setAvisarTelegram] = useState(camera?.avisarTelegram ?? true);
  const [parteImagem, setParteImagem] = useState<number>(camera?.parteImagem ?? ParteImagemCamera.Inteira);
  const [intervalo, setIntervalo] = useState(camera?.intervaloMinimoAlertasMinutos ?? INTERVALO_INICIAL);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  // Etapa 2 - origem da imagem. Câmera existente de tablet/navegador segue como está, a menos que o usuário troque a origem.
  const origemSalva: OrigemImagem = camera && ehCameraIp(camera) ? 'ip' : 'tablet';
  const [origem, setOrigem] = useState<OrigemImagem>(ipInicial ? 'ip' : origemSalva);
  const [equipamentoId, setEquipamentoId] = useState(camera?.equipamentoCameraId ?? ipInicial?.equipamentoId ?? '');
  const [canal, setCanal] = useState<number | null>(camera?.canal ?? ipInicial?.canal ?? null);
  const [stream, setStream] = useState<number>(camera?.stream ?? StreamCamera.Secundaria);
  const equipamentos = useEquipamentosCamera(obraId || undefined, !!obraId && origem === 'ip');
  const agentes = useAgentesCamera(obraId || undefined, !!obraId && origem === 'ip');
  const todasCameras = useCameras();
  const equipamentoEscolhido = (equipamentos.data ?? []).find((q) => q.id === equipamentoId);
  // Câmera IP sem a senha do equipamento pode ser salva, mas só desligada.
  const semSenhaNoEquipamento = origem === 'ip' && !!equipamentoEscolhido && !equipamentoEscolhido.senhaDefinida;
  const ligada = habilitada && !semSenhaNoEquipamento;

  const areas = useQuery({
    queryKey: ['areasSst', 'lista', obraId],
    queryFn: () => api.areasSst.listar(obraId),
    enabled: !!obraId,
  });
  const listaAreas = obraId ? (areas.data ?? []) : [];
  // Câmera antiga pode apontar para uma área que a consulta ainda não trouxe: mantém a opção para não perdê-la.
  const areaAtualFaltando = !!areaSstId && !listaAreas.some((a) => a.id === areaSstId);

  // Intervalo gravado fora da lista padrão continua selecionável em vez de ser trocado em silêncio.
  const intervalos = INTERVALOS_PADRAO.includes(intervalo)
    ? INTERVALOS_PADRAO
    : [...INTERVALOS_PADRAO, intervalo].sort((a, b) => a - b);

  async function salvar() {
    const nomeLimpo = nome.trim();
    if (!obraId) return setErro('Selecione a obra da câmera.');
    if (!nomeLimpo) return setErro('Informe o nome da câmera.');
    if (nomeLimpo.length > 100) return setErro('O nome da câmera pode ter no máximo 100 caracteres.');
    if (origem === 'ip') {
      if (!equipamentoId) return setErro('Escolha o equipamento da câmera IP.');
      if (canal === null) return setErro('Escolha a lente da câmera.');
    }

    const dados: DadosCamera = {
      obraId,
      nome: nomeLimpo,
      areaSstId: areaSstId || null,
      tipo,
      habilitada: ligada,
      intervaloMinimoAlertasMinutos: intervalo,
      avisarTelegram,
    };
    if (origem === 'ip') {
      dados.fonte = FonteCamera.Rtsp;
      dados.equipamentoCameraId = equipamentoId;
      dados.canal = canal;
      dados.stream = stream;
      dados.parteImagem = parteImagem;
    } else if (origemSalva === 'ip') {
      // Voltou para tablet/navegador: limpa equipamento, lente e qualidade. Câmera de tablet que continua de tablet não envia nada de origem.
      dados.fonte = FonteCamera.Navegador;
      dados.equipamentoCameraId = null;
      dados.canal = null;
      dados.stream = null;
      dados.parteImagem = ParteImagemCamera.Inteira;
    }
    try {
      setSalvando(true);
      setErro(null);
      if (camera) {
        const { obraId: _obra, ...semObra } = dados;
        void _obra;
        await api.cameras.atualizar(camera.id, semObra);
      } else {
        await api.cameras.criar(dados);
      }
      aoSalvar(obraId);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível salvar a câmera.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <FormSection titulo="Dados da câmera" numero={1} primeira>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <FormGrid>
        <Campo span={6}>
          <Field label="Obra" required>
            <Select
              value={obraId}
              disabled={edicao}
              onChange={(_, d) => {
                setObraId(d.value);
                setAreaSstId('');
                // Equipamento e lente são da obra anterior.
                setEquipamentoId('');
                setCanal(null);
              }}
            >
              <option value="">Selecione</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Nome" required>
            <Input
              value={nome}
              maxLength={100}
              placeholder="Ex.: Betoneira"
              onChange={(_, d) => setNome(d.value)}
            />
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Área">
            <Select value={areaSstId} disabled={!obraId} onChange={(_, d) => setAreaSstId(d.value)}>
              <option value="">Sem área definida</option>
              {areaAtualFaltando && <option value={areaSstId}>{camera?.areaNome ?? 'Área atual'}</option>}
              {listaAreas.map((area) => (
                <option key={area.id} value={area.id}>
                  {area.nome}
                </option>
              ))}
            </Select>
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Tipo" required>
            <Select value={String(tipo)} onChange={(_, d) => setTipo(Number(d.value))}>
              {[TipoCamera.Canteiro, TipoCamera.Portaria].map((t) => (
                <option key={t} value={t}>
                  {tipoCameraLabel[t]}
                </option>
              ))}
            </Select>
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Intervalo entre alertas">
            <Select value={String(intervalo)} onChange={(_, d) => setIntervalo(Number(d.value))}>
              {intervalos.map((minutos) => (
                <option key={minutos} value={minutos}>
                  {rotuloIntervalo(minutos)}
                </option>
              ))}
            </Select>
          </Field>
        </Campo>
        <Campo span={6}>
          <Switch
            checked={ligada}
            disabled={semSenhaNoEquipamento}
            label="Monitoramento ligado"
            onChange={(_, d) => setHabilitada(d.checked)}
          />
          {semSenhaNoEquipamento && (
            <div style={{ color: tokensUi.status.atencao.tinta, fontSize: 12, lineHeight: '16px', fontWeight: 500, marginTop: 6 }}>
              Para ligar, defina antes a senha do equipamento (Etapa 2).
            </div>
          )}
        </Campo>
        <Campo span={6}>
          <Switch
            checked={avisarTelegram}
            label="Avisar no Telegram"
            onChange={(_, d) => setAvisarTelegram(d.checked)}
          />
          <div className={ip.dica} style={{ fontSize: 12, lineHeight: '16px' }}>
            Desligado, a câmera continua analisando e registrando as ocorrências no sistema, só não envia mensagem no Telegram.
          </div>
        </Campo>
      </FormGrid>
      <FormSection titulo="Origem da imagem" numero={2}>
        <OrigemImagemCamera
          obraId={obraId}
          camera={camera}
          origem={origem}
          aoMudarOrigem={setOrigem}
          equipamentoId={equipamentoId}
          aoMudarEquipamento={setEquipamentoId}
          canal={canal}
          aoMudarCanal={setCanal}
          stream={stream}
          aoMudarStream={setStream}
          parteImagem={parteImagem}
          aoMudarParteImagem={setParteImagem}
          equipamentos={equipamentos.data ?? []}
          agentes={agentes.data ?? []}
          todasCameras={todasCameras.data ?? []}
        />
      </FormSection>
      <FormRodape>
        <Button onClick={aoCancelar} disabled={salvando}>
          Cancelar
        </Button>
        <Button appearance="primary" onClick={salvar} disabled={salvando}>
          {edicao ? 'Salvar alterações' : 'Cadastrar câmera'}
        </Button>
      </FormRodape>
    </FormSection>
  );
}
