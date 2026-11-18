import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Button, Campo, FeedbackInline, Field, FormGrid, Input, Select, StatusChip, useTipografia } from '@ui';
import { Add20Regular, Camera20Regular, Key20Regular } from '@fluentui/react-icons';
import { api, type AgenteCamera, type EquipamentoCamera } from '../../lib/api';
import { mergeClasses, useEstilosCameraIp } from './estilosCameraIp';

const PORTA_RTSP_PADRAO = '554';
const PORTA_ONVIF_PADRAO = '8899';

function lerPorta(texto: string, nome: string): number | string {
  const valor = Number(texto.trim());
  if (!Number.isInteger(valor) || valor < 1 || valor > 65535) return `A porta ${nome} precisa estar entre 1 e 65535.`;
  return valor;
}

export interface FormularioEquipamentoProps {
  // Presente = edição. A senha é definida na criação ou trocada pelo painel próprio; na edição só é pedida de novo quando o endereço muda.
  equipamento?: EquipamentoCamera;
  agentes: AgenteCamera[];
  // Cadastro dentro do formulário da câmera: só os campos essenciais e só os agentes da obra da câmera.
  obraId?: string;
  completo?: boolean;
  aoSalvar: (equipamentoId: string) => void;
  aoCancelar: () => void;
  aoTrocarSenha?: () => void;
}

// Equipamento = a câmera física (IP, usuário e senha). Usado no cadastro rápido da etapa "Origem da imagem"
// e na aba "Equipamentos e agentes" (com porta ONVIF e endereço MAC).
export function FormularioEquipamento({
  equipamento,
  agentes,
  obraId,
  completo,
  aoSalvar,
  aoCancelar,
  aoTrocarSenha,
}: FormularioEquipamentoProps) {
  const e = useEstilosCameraIp();
  const tipo = useTipografia();
  const queryClient = useQueryClient();
  const edicao = !!equipamento;

  // Agente revogado não serve para equipamento novo; na edição o atual continua na lista para não ser trocado em silêncio.
  const opcoesAgente = agentes.filter(
    (a) => (a.ativo || a.id === equipamento?.agenteCameraId) && (!obraId || a.obraId === obraId),
  );

  const [nome, setNome] = useState(equipamento?.nome ?? '');
  const [agenteId, setAgenteId] = useState(equipamento?.agenteCameraId ?? (opcoesAgente.length === 1 ? opcoesAgente[0].id : ''));
  const [host, setHost] = useState(equipamento?.host ?? '');
  const [portaRtsp, setPortaRtsp] = useState(String(equipamento?.portaRtsp ?? PORTA_RTSP_PADRAO));
  const [portaOnvif, setPortaOnvif] = useState(String(equipamento?.portaOnvif ?? PORTA_ONVIF_PADRAO));
  const [mac, setMac] = useState(equipamento?.enderecoMac ?? '');
  const [usuario, setUsuario] = useState(equipamento?.usuario ?? '');
  const [senha, setSenha] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  // Mudar o endereço de um equipamento existente exige digitar a senha de novo (o servidor também recusa sem ela).
  const enderecoMudou = !!equipamento && host.trim().toLowerCase() !== equipamento.host.trim().toLowerCase();
  const [salvando, setSalvando] = useState(false);

  async function salvar() {
    const nomeLimpo = nome.trim();
    const agente = agentes.find((a) => a.id === agenteId);
    if (!nomeLimpo) return setErro('Informe o nome do equipamento.');
    if (nomeLimpo.length > 100) return setErro('O nome do equipamento pode ter no máximo 100 caracteres.');
    if (!agente) return setErro('Escolha o agente (PC da obra) que lê esta câmera.');
    if (!host.trim()) return setErro('Informe o endereço IP da câmera.');
    if (!usuario.trim()) return setErro('Informe o usuário da câmera.');
    if (!edicao && !completo && !senha) return setErro('Informe a senha da câmera.');
    if (enderecoMudou && !senha) return setErro('Informe a senha novamente ao trocar o endereço da câmera.');
    const rtsp = lerPorta(portaRtsp, 'de vídeo');
    if (typeof rtsp === 'string') return setErro(rtsp);
    const onvif = lerPorta(portaOnvif, 'ONVIF');
    if (typeof onvif === 'string') return setErro(onvif);

    const dados = {
      agenteCameraId: agente.id,
      nome: nomeLimpo,
      host: host.trim(),
      portaRtsp: rtsp,
      portaOnvif: onvif,
      enderecoMac: mac.trim() || null,
      usuario: usuario.trim(),
      fabricante: equipamento?.fabricante ?? null,
      modelo: equipamento?.modelo ?? null,
    };
    try {
      setSalvando(true);
      setErro(null);
      let id: string;
      if (equipamento) {
        await api.cameras.atualizarEquipamento(equipamento.id, { ...dados, senha: enderecoMudou ? senha : null });
        id = equipamento.id;
      } else {
        id = (await api.cameras.criarEquipamento({ ...dados, obraId: agente.obraId, senha: senha || null })).id;
      }
      setSenha('');
      await queryClient.invalidateQueries({ queryKey: ['cameras', 'equipamentos'] });
      await queryClient.invalidateQueries({ queryKey: ['cameras', 'lista'] });
      aoSalvar(id);
    } catch (ex) {
      setErro(ex instanceof Error ? ex.message : 'Não foi possível salvar o equipamento.');
    } finally {
      setSalvando(false);
    }
  }

  return (
    <div className={e.subPainel}>
      <div className={e.subPainelTitulo}>
        {edicao ? <Camera20Regular aria-hidden="true" /> : <Add20Regular aria-hidden="true" />}
        {edicao ? `Editar "${equipamento.nome}"` : 'Novo equipamento'}
      </div>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <FormGrid>
        <Campo span={6}>
          <Field label="Nome do equipamento" required dica="Nome que a equipe reconhece, como o local onde a câmera está.">
            <Input value={nome} maxLength={100} placeholder="Ex.: Câmera A31 — Guarita" onChange={(_, d) => setNome(d.value)} />
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Agente (PC da obra)" required dica="O PC da obra que lê esta câmera pela rede local.">
            <Select value={agenteId} onChange={(_, d) => setAgenteId(d.value)}>
              <option value="">Selecione</option>
              {opcoesAgente.map((a) => (
                <option key={a.id} value={a.id}>
                  {obraId ? a.nome : `${a.nome} — ${a.obraNome}`}
                </option>
              ))}
            </Select>
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Endereço IP" required dica="Só o IP ou o nome da câmera na rede da obra, sem rtsp://, barras ou espaços.">
            <Input
              className={e.mono}
              value={host}
              inputMode="decimal"
              placeholder="192.168.1.80"
              onChange={(_, d) => setHost(d.value)}
            />
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Porta de vídeo" dica="Porta RTSP da câmera. O padrão é 554.">
            <Input className={e.mono} value={portaRtsp} inputMode="numeric" onChange={(_, d) => setPortaRtsp(d.value)} />
          </Field>
        </Campo>
        <Campo span={6}>
          <Field label="Usuário" required dica="Usuário de acesso à câmera.">
            <Input value={usuario} maxLength={100} placeholder="Usuário da câmera" onChange={(_, d) => setUsuario(d.value)} />
          </Field>
        </Campo>
        <Campo span={6}>
          {edicao ? (
            <Field
              label="Senha"
              dica="A senha só é gravada. Depois de salva, não aparece mais em nenhuma tela."
              hint="A senha só é gravada. Depois de salva, não aparece mais em nenhuma tela."
            >
              <div className={e.senhaLinha}>
                {equipamento.senhaDefinida ? (
                  <StatusChip tom="ok">Senha definida</StatusChip>
                ) : (
                  <StatusChip tom="atencao">Senha ainda não definida</StatusChip>
                )}
                {aoTrocarSenha && (
                  <Button className={e.botao} icon={<Key20Regular />} onClick={aoTrocarSenha}>
                    {equipamento.senhaDefinida ? 'Trocar senha' : 'Definir senha'}
                  </Button>
                )}
              </div>
            </Field>
          ) : (
            <Field
              label="Senha"
              required={!completo}
              dica="A senha é guardada protegida e nunca mais aparece em nenhuma tela."
              hint={
                completo
                  ? 'A senha só é gravada. Depois de salva, não aparece mais em nenhuma tela. Câmeras de fábrica costumam vir sem senha: defina uma na própria câmera e informe aqui.'
                  : 'A senha é guardada protegida e nunca mais aparece em nenhuma tela.'
              }
            >
              <Input
                type="password"
                value={senha}
                autoComplete="new-password"
                placeholder="Digite a senha da câmera"
                onChange={(_, d) => setSenha(d.value)}
              />
            </Field>
          )}
        </Campo>
        {enderecoMudou && (
          <Campo span={6}>
            <Field
              label="Senha da câmera"
              required
              dica="Você mudou o endereço: digite a senha da câmera de novo. Ela é guardada protegida e nunca mais aparece em nenhuma tela."
              hint="Você mudou o endereço: digite a senha da câmera de novo. Ela é guardada protegida e nunca mais aparece em nenhuma tela."
            >
              <Input
                type="password"
                value={senha}
                autoComplete="new-password"
                placeholder="Digite a senha da câmera"
                onChange={(_, d) => setSenha(d.value)}
              />
            </Field>
          </Campo>
        )}
        {completo && (
          <>
            <Campo span={4}>
              <Field label="Porta ONVIF (opcional)" dica="Usada para encontrar a câmera na rede. O padrão é 8899.">
                <Input className={e.mono} value={portaOnvif} inputMode="numeric" onChange={(_, d) => setPortaOnvif(d.value)} />
              </Field>
            </Campo>
            <Campo span={8}>
              <Field
                label="Endereço MAC (opcional)"
                dica="Ajuda o agente a reencontrar a câmera se o endereço IP mudar."
                hint="Ajuda o agente a reencontrar a câmera se o endereço IP mudar."
              >
                <Input className={e.mono} value={mac} placeholder="00:12:34:AB:CD:EF" onChange={(_, d) => setMac(d.value)} />
              </Field>
            </Campo>
          </>
        )}
      </FormGrid>
      <div className={e.acoesLinha}>
        <Button appearance="primary" className={e.botao} onClick={() => void salvar()} disabled={salvando}>
          Salvar equipamento
        </Button>
        <Button className={e.botao} onClick={aoCancelar} disabled={salvando}>
          Cancelar
        </Button>
      </div>
      {!completo && !edicao && (
        <div className={mergeClasses(tipo.legenda, e.dica)}>Dá para fazer o mesmo na aba "Equipamentos e agentes", com mais opções.</div>
      )}
    </div>
  );
}
