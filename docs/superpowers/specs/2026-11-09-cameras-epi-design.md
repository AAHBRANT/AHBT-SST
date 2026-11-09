# Câmeras — detecção de EPI por imagem — G-SST

Status: desenho da fase 1 aprovado pelo usuário em 09/11/2026. Nenhuma implementação iniciada.

## 1. Decisões aprovadas

- Novo módulo **Câmeras** no G-SST: o sistema analisa imagens de câmeras da obra, detecta pessoas sem EPI e dispara alerta automático no grupo do Telegram.
- EPIs detectados: **capacete** e **colete**. **Luvas estão fora do escopo** (decisão do usuário).
- **Fase 1 — alerta por área:** o alerta informa obra, câmera, data/hora e foto. O sistema não identifica o trabalhador; o técnico pode **vincular manualmente** o trabalhador à ocorrência depois.
- **Fase 2 — identificação por rosto** em câmera próxima (portaria/passagem), usando a galeria facial da obra. Quando não reconhecer, o alerta sai como "trabalhador não identificado". Fora desta especificação; depende de validação do jurídico/DPO.
- **Piloto pela câmera do tablet**, numa tela do próprio SST. A câmera fixa da obra entra depois como outra fonte de imagem, sem refazer o módulo.
- **Análise só quando há movimento** na cena, com teto de uma foto analisada a cada 30–60 s por câmera.
- **Alerta a cada ocorrência detectada**, sem intervalo mínimo. O intervalo mínimo entre alertas fica como configuração por câmera, com valor inicial "sem limite".
- **Motor de IA: Claude** analisando fotos (regra do projeto: IA somente Claude). Modelo próprio de detecção (tipo YOLO) em mini-PC foi descartado para esta fase.

## 2. Fluxo

1. O técnico abre **Câmeras → Monitorar** no tablet, escolhe uma câmera cadastrada e inicia o monitoramento.
2. O navegador compara quadros consecutivos da câmera do dispositivo (diferença de pixels em imagem reduzida). Sem movimento acima do limiar, nada é enviado.
3. Com movimento, e respeitado o intervalo mínimo entre análises, o navegador envia um quadro JPEG ao servidor.
4. O servidor envia a imagem ao Claude com um pedido de resposta estruturada: número de pessoas visíveis, número sem capacete, número sem colete e grau de confiança.
5. Se houver pelo menos uma pessoa sem capacete ou sem colete, o servidor grava a ocorrência com a foto e envia o alerta ao Telegram, respeitado o intervalo mínimo entre alertas da câmera.
6. Se não houver irregularidade, a foto é descartada e nada é gravado além da contagem de análises.

## 3. Componentes

| Unidade | Responsabilidade | Depende de |
| --- | --- | --- |
| Cadastro de câmeras | CRUD de câmera: nome, obra, área, tipo (canteiro/portaria), ativa, intervalo mínimo entre alertas | Obras e áreas existentes; escopo por obra |
| Fonte de imagem (frontend) | Entregar quadros de uma câmera. Fase 1: câmera do dispositivo (`getUserMedia`). Depois: câmera IP da obra | — |
| Detector de movimento (frontend) | Decidir se um quadro merece análise; aplicar o teto de frequência | Fonte de imagem |
| Analisador de EPI (backend) | Receber uma imagem e devolver o resultado estruturado. Interface própria (`IAnalisadorEpiImagem`), implementada com Claude | Cliente Claude já configurado (`ClaudeOptions`) |
| Registro de ocorrências | Gravar ocorrência, foto e vínculo manual do trabalhador | Analisador; escopo por obra |
| Notificador | Montar legenda e enviar foto ao Telegram | `ITelegramResumoService.EnviarImagemAsync` |

A interface do analisador isola a IA: a fase 2 (rosto) e uma eventual troca de motor não alteram cadastro, registro nem notificação.

## 4. Dados

**Câmera:** nome, obra, área (opcional), tipo, ativa, intervalo mínimo entre alertas em minutos (0 = sem limite), auditoria padrão.

**Ocorrência de EPI:** câmera, obra, data/hora da captura, data/hora do recebimento, foto, pessoas na cena, sem capacete, sem colete, confiança informada pela IA, alerta enviado (sim/não e quando), trabalhador vinculado (opcional), quem vinculou e quando, situação (nova / conferida / descartada como falso alerta).

Fotos sem irregularidade não são armazenadas.

## 5. Telas

- **Câmeras:** lista e cadastro.
- **Monitorar:** imagem ao vivo, estado "monitorando", última análise, contador de ocorrências do dia, botão iniciar/parar.
- **Ocorrências:** lista com foto, obra, câmera, horário e o que faltou; filtros por obra, câmera e período; ações **Vincular trabalhador** e **Marcar como falso alerta**.

O visual das telas será apresentado em mockup HTML e aprovado antes do código (regra do projeto).

## 6. Mensagem do Telegram

```
⚠️ EPI — pessoa sem capacete
Obra Beira Rio · Câmera Betoneira · 09/11 15:06
2 pessoas na cena, 1 sem capacete
```

Enviada como foto com legenda, para o grupo já usado pelo resumo do DDS. Um grupo próprio para câmeras seria uma configuração adicional, não prevista nesta fase.

## 7. Regras e tratamento de erro

- Acesso segue o escopo por obra existente: cada usuário vê apenas câmeras e ocorrências das suas obras. Exclusão segue a regra "somente Admin".
- Falha ou resposta inválida do Claude: a análise é descartada e registrada em log; não gera ocorrência nem alerta.
- Falha no envio ao Telegram: a ocorrência permanece gravada com "alerta não enviado".
- Sem configuração de Telegram ou de Claude: o monitoramento informa na tela que o recurso está indisponível.
- O endpoint de análise valida tamanho e tipo da imagem, exige câmera ativa e aplica no servidor o teto de frequência por câmera (o limite do navegador não é suficiente).

## 8. Limites conhecidos

- A IA pode errar nos dois sentidos (boné, capuz, ângulo, distância, iluminação). O alerta sempre leva a foto para conferência humana; a taxa de acerto será medida no piloto.
- No piloto, o tablet precisa manter a tela de monitoramento aberta; bloqueio de tela interrompe a captura.
- A conexão com a câmera fixa da obra não é validada pelo piloto no tablet. Marca, modelo e protocolo da câmera existente ainda não foram informados; câmeras de baixo custo podem não expor RTSP/ONVIF.
- Custo por foto analisada: será medido no piloto e apresentado ao usuário antes de ativar várias câmeras.

## 9. LGPD

As fotos de ocorrência mostram trabalhadores, mesmo sem reconhecimento facial. Antes do uso real em obra: aviso de monitoramento no local e validação, com o jurídico/DPO, da base legal e do prazo de guarda das fotos. A fase 2 trata dado biométrico e exige validação específica.

## 10. Testes

- Analisador: testes com a resposta do Claude simulada (irregularidade, cena limpa, resposta inválida, falha).
- Registro e notificação: ocorrência gravada, intervalo mínimo entre alertas, falha do Telegram, escopo por obra.
- Endpoint: câmera inativa, imagem inválida, teto de frequência.
- Frontend: detector de movimento com quadros sintéticos.
- Piloto real no tablet com pessoas com e sem capacete/colete, registrando acertos e erros.

## 11. Fora do escopo desta fase

Luvas e demais EPIs; reconhecimento facial; câmera IP/RTSP da obra; análise de vídeo contínuo em tempo real; relatório periódico de ocorrências; grupo de Telegram separado.
