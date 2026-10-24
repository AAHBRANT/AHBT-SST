# Consentimento para tratamento de dados biométricos — RASCUNHO PARA REVISÃO JURÍDICA

> **Status: rascunho técnico. Não é texto jurídico final.** Serve para o jurídico/encarregado (DPO) validar e
> reescrever. Nada aqui foi decidido juridicamente; os campos entre `[colchetes]` precisam ser preenchidos por
> quem tem competência. Riscos legais estão marcados como **ATENÇÃO**.

## 1. O que o sistema faz de fato (para embasar o texto)

O sistema **não mostra** texto de consentimento ao trabalhador: ele registra a data em que o operador confirmou
que o **termo físico** foi assinado (`Trabalhador.TermoAceiteAssinaturaEletronicaEm` e `ConsentimentoBiometriaEm`).
Portanto, o termo físico precisa refletir o funcionamento real abaixo.

| Tema | Como funciona hoje |
|---|---|
| Finalidade | Identificar o trabalhador e registrar **assinatura eletrônica** em documentos de SST (DDS, treinamentos, EPI/uniforme, PT, inspeção, APR) e **confirmar presença** em DDS/treinamentos. |
| Dados — digital | A imagem da digital é lida pelo leitor Futronic e convertida em um **modelo matemático (template)**. O template é guardado **criptografado (AES-256-GCM)** no banco da AAHBRANT. Um **agente instalado no PC da obra** baixa os templates da obra, descriptografa **em memória** e compara. A imagem bruta **não** é guardada. |
| Dados — facial | A **foto do rosto** é enviada ao serviço **Microsoft Azure Face API** para cadastro e para reconhecimento. As fotos do cadastro (aprovadas) ficam **guardadas no perfil do trabalhador**, com hash SHA-256. Em cada assinatura por facial, a foto da ocasião também fica como evidência. |
| Quem acessa | Digital: agentes das obras (só templates da própria obra) e a API. Fotos do cadastro facial: usuários com a permissão de assinatura. |
| Registro da assinatura | Data/hora, método (digital ou facial), IP e o documento assinado, na trilha de auditoria. |
| Retenção | **Não há prazo definido no sistema hoje.** Templates e fotos permanecem até exclusão. |

**ATENÇÃO — divergência com a documentação anterior:** o desenho original (`docs/Motor-Assinatura-Eletronica.md`,
seção 4) prometia "só o template, nunca a imagem bruta, e o template **não sai do leitor**", e um método de reserva
(crachá + PIN). **Nenhuma das duas coisas vale mais**: o template fica no banco (criptografado) e num cache do
agente, e não existe método de assinatura sem biometria (crachá/PIN foram removidos em 31/08).

## 2. Pontos que o jurídico precisa decidir

1. **Base legal.** O rascunho abaixo usa **consentimento** (LGPD art. 11, I). Avaliar se cabe outra base, como
   **garantia da prevenção à fraude e da segurança do titular nos processos de identificação e autenticação de
   cadastro em sistemas eletrônicos** (art. 11, II, "g"), e se a relação de emprego enfraquece a "livre
   manifestação" do consentimento.
2. **ATENÇÃO — ausência de alternativa.** Como não há método sem biometria, o trabalhador que não consentir não
   consegue assinar pelo sistema. Definir qual é o procedimento alternativo (ex.: assinatura física) e escrevê-lo
   no termo, sob pena de o consentimento não ser considerado livre.
3. **Transferência internacional / operador.** A foto facial é processada pela Microsoft (Azure). Confirmar região
   do serviço, contrato/DPA e a necessidade de citar a transferência internacional (LGPD arts. 33 e seguintes).
4. **Prazo de retenção e descarte** (ex.: ao desligar o trabalhador ou ao fim da obra) e quem executa o descarte.
5. **Encarregado (DPO)** e canal de atendimento ao titular.
6. **Relatório de Impacto (RIPD)** para tratamento de dado sensível em larga escala, se aplicável.
7. Necessidade de **dois termos** (assinatura eletrônica e biometria) ou um só, como está hoje no cadastro.

## 3. Texto proposto (base para o jurídico reescrever)

**TERMO DE CONSENTIMENTO PARA TRATAMENTO DE DADOS BIOMÉTRICOS**

Eu, **[nome do trabalhador]**, CPF **[CPF]**, matrícula **[matrícula]**, vinculado(a) à obra **[obra]**, declaro
que fui informado(a) e, de forma **livre, informada e inequívoca**, autorizo a **[razão social — CNPJ]**
("AAHBRANT"), controladora dos dados, a tratar meus dados pessoais sensíveis descritos abaixo.

1. **Dados tratados.**
   a) **Impressão digital**: convertida em modelo matemático (template) a partir da leitura do leitor biométrico.
      A imagem da digital não é armazenada.
   b) **Imagem do rosto (foto)**: usada para o cadastro e para o reconhecimento facial.
2. **Finalidade.** Identificar-me e registrar minha assinatura eletrônica em documentos de segurança e saúde no
   trabalho (como DDS, treinamentos, entrega de EPI/uniforme, permissão de trabalho, APR e inspeções) e confirmar
   minha presença em atividades da obra. Os dados **não serão usados para outra finalidade** sem novo consentimento.
3. **Como os dados são guardados.** O modelo da digital é armazenado de forma **criptografada** nos sistemas da
   AAHBRANT e é usado por programa instalado no computador da obra para comparação. As fotos do meu cadastro
   facial ficam **registradas no meu perfil**, e cada assinatura por reconhecimento facial guarda a foto da
   ocasião como evidência.
4. **Compartilhamento.** A foto facial é processada pelo serviço **Microsoft Azure Face API**, na condição de
   operador, **[região/país e base contratual — a preencher pelo jurídico]**. Não há venda nem cessão a
   terceiros para outras finalidades.
5. **Prazo.** Os dados serão mantidos **[prazo/critério de retenção — a definir]** e descartados **[regra de
   descarte — a definir]**.
6. **Meus direitos.** Posso, a qualquer momento e mediante solicitação a **[canal / encarregado — a preencher]**:
   confirmar o tratamento; acessar meus dados; corrigi-los; pedir a eliminação; revogar este consentimento; e
   obter informações sobre com quem os dados são compartilhados.
7. **Alternativa.** Se eu **não** consentir, poderei assinar e confirmar presença por **[procedimento alternativo
   — a definir]**, sem prejuízo ao meu vínculo.
8. **Revogação.** Posso revogar este consentimento a qualquer momento. A revogação não afeta a validade das
   assinaturas já registradas com base nele, e a AAHBRANT eliminará meus dados biométricos nos termos do item 5.

Local e data: **[local]**, **[data]**.

Assinatura: ______________________________________

## 4. Textos associados que também precisam de revisão

- **Termo de Aceite de Assinatura Eletrônica** (MP 2.200-2/2001, art. 10, §2º): aceitação de assinatura eletrônica
  fora do ICP-Brasil, incluindo que a assinatura registra data, hora, documento, método e trilha de auditoria.
- **Tela de cadastro de digital** (`CadastroDigitalDialog`): hoje diz que o operador confirma que os termos físicos
  foram assinados; ajustar assim que o texto final existir.
- **Documentação de projeto** (`docs/Motor-Assinatura-Eletronica.md`, seções 1 e 4): desatualizada quanto a
  "template não sai do leitor" e ao método de reserva com crachá/PIN.
