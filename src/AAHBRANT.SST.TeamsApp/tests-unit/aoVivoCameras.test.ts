// Regras e mensagens do "Ver ao vivo" da câmera IP. Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { desfechoDaRecusa, desfechoDoEncerramento } from '../src/pages/cameras/aoVivoMensagens.ts';
import { bloqueioAoVivo, formatarContagem, rotuloAlertaTelegram, versaoAtendeAoVivo } from '../src/pages/cameras/aoVivoRegras.ts';
import { mensagemDaFalhaAoVivo } from '../src/pages/cameras/mensagensTesteConexao.ts';

test('versão do serviço da obra: 1.1.0 em diante abre o ao vivo', () => {
  assert.equal(versaoAtendeAoVivo('1.1.0'), true);
  assert.equal(versaoAtendeAoVivo('1.1.4'), true);
  assert.equal(versaoAtendeAoVivo('1.2'), true);
  assert.equal(versaoAtendeAoVivo('2.0.0'), true);
  assert.equal(versaoAtendeAoVivo('1.0.9'), false);
  assert.equal(versaoAtendeAoVivo('0.9.0'), false);
});

test('versão ausente ou ilegível não bloqueia: quem decide é o servidor', () => {
  assert.equal(versaoAtendeAoVivo(null), null);
  assert.equal(versaoAtendeAoVivo(undefined), null);
  assert.equal(versaoAtendeAoVivo('abc'), null);
});

test('bloqueio do botão segue a ordem senha, agente sem contato, serviço desatualizado', () => {
  const livre = { equipamentoSemSenha: false, agenteSemContato: false, versaoDoAgente: '1.1.3' };
  assert.equal(bloqueioAoVivo(livre), null);
  assert.equal(bloqueioAoVivo({ ...livre, versaoDoAgente: null }), null);
  assert.equal(bloqueioAoVivo({ ...livre, versaoDoAgente: '1.0.0' }), 'versao');
  assert.equal(bloqueioAoVivo({ ...livre, agenteSemContato: true, versaoDoAgente: '1.0.0' }), 'agente');
  assert.equal(bloqueioAoVivo({ ...livre, equipamentoSemSenha: true, agenteSemContato: true }), 'senha');
});

test('contador mm:ss', () => {
  assert.equal(formatarContagem(400), '6:40');
  assert.equal(formatarContagem(65), '1:05');
  assert.equal(formatarContagem(9), '0:09');
  assert.equal(formatarContagem(-3), '0:00');
});

test('recusa ao abrir é escolhida só pelo código', () => {
  assert.equal(desfechoDaRecusa(429, 'limite_sessoes').fase, 'limite');
  assert.equal(desfechoDaRecusa(429, null).fase, 'limite');
  assert.equal(desfechoDaRecusa(404, null).fase, 'falha');
  assert.equal(desfechoDaRecusa(409, 'agente_desatualizado').mensagem?.titulo, 'O serviço da obra precisa ser atualizado.');
  assert.equal(desfechoDaRecusa(409, 'agente_sem_contato').mensagem?.titulo, 'O agente da obra não respondeu.');
  assert.equal(desfechoDaRecusa(409, 'camera_desabilitada').fase, 'falha');
  // Código novo (servidor mais novo que a tela): mensagem neutra, sem inventar causa.
  assert.equal(desfechoDaRecusa(409, 'codigo_futuro').mensagem?.titulo, 'Não foi possível abrir a imagem.');
});

test('encerramento do quadro: tempo, inatividade, PC da obra e falha da câmera', () => {
  assert.equal(desfechoDoEncerramento('tempo_maximo').fase, 'tempo');
  assert.equal(desfechoDoEncerramento('inatividade').fase, 'inatividade');
  const limite = desfechoDoEncerramento('limite_sessoes');
  assert.equal(limite.fase, 'limite');
  assert.equal(limite.mensagem?.titulo, 'O máximo de câmeras ao vivo ao mesmo tempo foi atingido.');
  assert.equal(limite.mensagem?.texto, 'Feche uma transmissão e tente de novo.');
  assert.equal(desfechoDoEncerramento('encerrada').fase, 'encerrada');
  assert.equal(desfechoDoEncerramento(null).fase, 'encerrada');
  const parou = desfechoDoEncerramento('agente_sem_resposta');
  assert.equal(parou.fase, 'falha');
  assert.match(parou.mensagem?.titulo ?? '', /computador da obra parou de enviar a imagem/);
  const senha = desfechoDoEncerramento('falha:senha_recusada');
  assert.equal(senha.fase, 'falha');
  assert.equal(senha.mensagem?.titulo, 'A câmera não aceitou o usuário ou a senha.');
});

test('falha ao vivo troca "conferir lente/qualidade" por "Confira a câmera no cadastro."', () => {
  assert.equal(mensagemDaFalhaAoVivo('sem_imagem').texto, 'Confira a câmera no cadastro.');
  assert.equal(mensagemDaFalhaAoVivo('imagem_ilegivel').texto, 'Confira a câmera no cadastro.');
  assert.equal(mensagemDaFalhaAoVivo('canal_ou_stream_invalido').texto, 'Confira a câmera no cadastro.');
  assert.equal(mensagemDaFalhaAoVivo('erro_desconhecido').texto, 'Confira a câmera no cadastro.');
  assert.equal(mensagemDaFalhaAoVivo(null).texto, 'Confira a câmera no cadastro.');
  // Mensagens que não falam de lente nem de qualidade ficam como no teste de conexão.
  assert.equal(
    mensagemDaFalhaAoVivo('camera_nao_encontrada').texto,
    'Confira o IP e a porta. A câmera precisa estar ligada e na mesma rede do PC da obra.',
  );
  assert.equal(mensagemDaFalhaAoVivo('senha_recusada').texto, 'Confira os dados do equipamento e tente de novo.');
});

test('aviso no Telegram: câmera com aviso desligado não é "alerta não enviado"', () => {
  assert.deepEqual(rotuloAlertaTelegram(true, true), { texto: 'Alerta enviado ao Telegram', tom: 'ok' });
  assert.deepEqual(rotuloAlertaTelegram(false, false), { texto: 'Sem aviso no Telegram', tom: 'neutro' });
  assert.deepEqual(rotuloAlertaTelegram(false, true), { texto: 'Alerta não enviado', tom: 'aviso' });
  assert.deepEqual(rotuloAlertaTelegram(false, undefined), { texto: 'Alerta não enviado', tom: 'aviso' });
});
