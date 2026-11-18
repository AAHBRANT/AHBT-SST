// Mensagens do teste de conexão da câmera IP. Rodar: node --test tests-unit/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { CODIGOS_TESTE_CONEXAO, mensagemDaFalha } from '../src/pages/cameras/mensagensTesteConexao.ts';

const falha = (codigo: string | null, motivo: string | null = null, detalhe: string | null = null, situacao = 'Concluido') =>
  mensagemDaFalha({ situacao, codigo, motivo, detalhe });

test('pedido expirado orienta a conferir o PC da obra', () => {
  assert.equal(falha(null, null, null, 'Expirado').titulo, 'O agente da obra não respondeu.');
});

test('cada código tem a mensagem do mockup aprovado', () => {
  assert.equal(falha('senha_recusada').titulo, 'A câmera não aceitou o usuário ou a senha.');
  assert.equal(falha('camera_nao_encontrada').titulo, 'Não encontramos a câmera nesse endereço.');
  assert.equal(falha('sem_imagem').titulo, 'A câmera respondeu, mas não entregou imagem.');
  assert.equal(falha('imagem_ilegivel').titulo, 'Não conseguimos abrir o formato da imagem.');
});

test('textos reais do agente não decidem a causa: só o código decide', () => {
  // "não respondeu a tempo" com detalhe "Sem imagem em 25 s." era lido como "respondeu, mas não entregou imagem".
  const semResposta = falha('sem_resposta', 'A câmera não respondeu a tempo', 'Sem imagem em 25 s.');
  assert.equal(semResposta.titulo, 'A câmera não respondeu a tempo.');
  assert.equal(semResposta.detalhe, 'Sem imagem em 25 s.');

  // "ffmpeg não encontrado" era lido como "câmera não encontrada no endereço".
  const ffmpeg = falha('ffmpeg_ausente', 'O agente não encontrou o ffmpeg neste computador', 'Confira AgenteCameras:CaminhoFfmpeg no computador do agente.');
  assert.equal(ffmpeg.titulo, 'O agente não conseguiu ler imagens neste PC.');
  assert.match(ffmpeg.texto, /instalação do agente/);
  assert.doesNotMatch(`${ffmpeg.titulo} ${ffmpeg.texto}`, /endereço|IP|porta/);

  // "encerrou a conexão" era lido como "senha recusada".
  const encerrada = falha('conexao_encerrada', 'A câmera encerrou a conexão (confira usuário, senha, canal e stream)');
  assert.equal(encerrada.titulo, 'A câmera encerrou a conexão.');
  assert.doesNotMatch(encerrada.titulo, /senha/i);

  assert.equal(falha('canal_ou_stream_invalido', 'A câmera não tem esse canal ou stream').titulo, 'A câmera não tem essa lente ou qualidade.');
  assert.equal(falha('conexao_recusada', 'A câmera recusou a conexão (confira a porta RTSP)').titulo, 'A câmera recusou a conexão.');
  assert.equal(falha('acesso_negado').titulo, 'A câmera negou o acesso deste usuário.');
});

test('todos os códigos conhecidos têm mensagem própria e distinta', () => {
  const titulos = new Set(CODIGOS_TESTE_CONEXAO.map((c) => falha(c).titulo));
  assert.equal(titulos.size, CODIGOS_TESTE_CONEXAO.length);
});

test('sem código, com código desconhecido ou erro_desconhecido: mensagem neutra com o motivo em detalhe', () => {
  for (const codigo of [null, 'inventado_pelo_futuro', 'erro_desconhecido']) {
    const m = falha(codigo, 'erro x', null);
    assert.equal(m.titulo, 'Não foi possível ler a imagem da câmera.');
    assert.equal(m.detalhe, 'erro x');
  }
});

test('texto com palavra-chave de outra causa não muda a mensagem do código', () => {
  const m = falha('erro_desconhecido', '401 unauthorized senha host rede');
  assert.equal(m.titulo, 'Não foi possível ler a imagem da câmera.');
});

test('nenhuma mensagem usa a palavra vencido', () => {
  for (const codigo of [...CODIGOS_TESTE_CONEXAO, null]) {
    const m = falha(codigo);
    assert.ok(!/venc/i.test(`${m.titulo} ${m.texto}`));
  }
});
