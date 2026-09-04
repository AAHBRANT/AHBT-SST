# Assinatura Eletrônica por Reconhecimento Facial (Azure Face API) — Design

**Data:** 2026-09-04
**Status:** aprovado em brainstorming, pronto para virar plano de implementação (`writing-plans`)

## 1. Objetivo

O usuário fará o cadastro prévio (enrollment) da face de cada colaborador. No dia a dia, o colaborador fica em frente à webcam/câmera do celular para assinar digitalmente documentos de SST (DDS e Ficha de EPI confirmados; PT/Inspeção/Treinamento herdam automaticamente por reaproveitarem o mesmo motor — ver seção 2). Precisa ter validade jurídica de Assinatura Eletrônica Avançada (Lei 14.063/2020), com trilha de auditoria para fiscalização do MTE. Fora de escopo nesta rodada: ASO (sem fluxo de assinatura hoje) e "Ordem de Serviço" (OS — documento que ainda não existe no sistema, feature futura separada).

## 2. Decisão central: método novo dentro do Motor de Assinatura já existente

O projeto já tem um Motor de Assinatura Eletrônica genérico e polimórfico (`DocumentoAssinatura`/`DocumentoSignatario`, `EntidadeTipo`/`EntidadeId`), hoje com dois métodos: `MetodoAutenticacaoAssinatura.Biometria` (leitor de digital Futronic FS80H, matching **local** no dispositivo) e `SessaoLogada`. `IRegistradorAssinaturaService.RegistrarAsync(documentoAssinaturaId, ResultadoAutenticacaoAssinatura, ipAddress, ct)` é o único ponto de entrada correto para gravar uma assinatura — qualquer estratégia de autenticação só precisa produzir um `ResultadoAutenticacaoAssinatura(TrabalhadorId, Metodo)` e chamar esse serviço, exatamente como o Futronic já faz.

**Decisão:** reconhecimento facial é um **método adicional** (`ReconhecimentoFacial`), não substitui o Futronic. Por reaproveitar o motor genérico, funciona automaticamente em **qualquer documento que já usa o Motor de Assinatura** (DDS, Ficha de EPI, PT, Inspeção, Treinamento) — não é trabalho por tipo de documento, é só o botão "assinar com reconhecimento facial" aparecendo na tela de cada um.

## 3. Novo componente: `IAutenticacaoFacialService`

Mesmo papel de `FutronicAutenticacaoStrategy`/`IAutenticacaoBiometriaLocalService`, mas chamando a Azure Face API em vez de hardware local:

- `IdentificarAsync(byte[] frameJpeg, Guid obraId, CancellationToken ct) -> ResultadoAutenticacaoFacial`
- Internamente: chama `Face - Identify` do Azure contra o `PersonGroup` da obra (um `PersonGroup` por obra, análogo ao escopo de `DispositivoAgenteBiometrico` por obra hoje), recebe `personId` + `confidence`, resolve `personId → TrabalhadorId` (mapeamento novo, ver seção 6), e devolve o resultado.
- Threshold: `confidence >= 0.85` → aceito; `0.60 <= confidence < 0.85` → rejeitado com mensagem "baixa confiança, tente novamente"; `< 0.60` ou nenhum rosto → "rosto não reconhecido"; mais de um rosto no frame → bloqueio explícito ("mais de uma pessoa na câmera").
- Erro de rede/Azure indisponível: não é tratado como "rosto não reconhecido" — cai no fluxo de fila offline (seção 5), mesmo código.

## 4. Cadastro (Enrollment)

Nova seção/botão na aba de perfil do trabalhador (não um item de menu novo — segue o padrão já estabelecido no projeto de "dado do trabalhador vira aba no perfil dele", nunca tabela nova no menu). Fluxo:

1. Aciona a webcam (`getUserMedia`), captura 1+ fotos do colaborador.
2. Cria (ou atualiza) o `Person` no Azure `PersonGroup` da obra, associa a(s) foto(s) capturada(s) (`Person - Add Face`).
3. Dispara `PersonGroup - Train` (Azure precisa treinar o grupo depois de adicionar rostos).
4. Persiste o mapeamento `TrabalhadorId ↔ AzurePersonId` (ver seção 6).

`Trabalhador.FotoConteudo` (já existe no projeto) é só uma prévia visual — não é reaproveitado como foto de treino do Azure (a API exige controle próprio sobre a imagem enviada ao `Person`).

## 5. Fluxo offline

Já existe um motor de sincronização offline (`src/lib/offline`, `syncEngine.ts`) piloto em DDS/Inspeções/Checklists/APRs: leituras em cache, mutações em fila local (IndexedDB), reenviadas quando a internet volta. Azure Face API é serviço de nuvem — não dá pra verificar rosto sem internet no instante da assinatura. Decisão do usuário: a foto fica **pendente de verificação**, não finge sucesso.

1. Offline: captura o frame, grava na fila local com status "pendente de verificação facial" (mesmo padrão de mutação enfileirada já usado no projeto).
2. UI mostra o documento como "assinatura pendente" — não como assinado.
3. Quando a internet volta, o motor de sincronização dispara `IdentificarAsync` de verdade contra o Azure.
4. Só então o `DocumentoSignatario` é de fato criado (com o score real do Azure) via `IRegistradorAssinaturaService.RegistrarAsync` — ou a tentativa é marcada como rejeitada, se o score não bater.

**Teams e o "modo campo":** o cliente Teams não abre a aba sem internet (carrega tudo via iframe do zero). Como o app já é um PWA instalável (`VitePWA` já configurado, service worker cacheia o app shell), a solução é: **uso em campo sem sinal acontece pelo PWA instalado direto no navegador do dispositivo** (fora do cliente Teams), não pela aba dentro do Teams. Teams continua sendo o ponto de entrada normal com internet.

## 6. Modelo de dados novo

- `Trabalhador.AzureFacePersonId` (`string?`, novo campo) — id do `Person` no Azure, gerado no enrollment.
- `Obra.AzureFacePersonGroupId` (`string?`, novo campo) — um `PersonGroup` por obra (Azure recomenda escopo por grupo para reduzir custo/tempo de `Identify` e evitar falsos positivos entre obras diferentes).
- `MetodoAutenticacaoAssinatura` ganha `ReconhecimentoFacial = 6` (próximo valor livre; `2`/`3`/`4` foram removidos por decisão anterior de PIN/crachá-QR/WebAuthn).
- Reaproveita a `TrilhaAuditoria` genérica já existente para registrar tentativas (sucesso e falha) — sem tabela nova de auditoria.
- Fila offline de fotos pendentes: reaproveita `offlineDb` (IndexedDB) já existente no frontend — nova store `fotosFaciaisPendentes` (foto + entidade alvo + timestamp de captura).

## 7. Configuração/custo (Azure Face API, tier F0)

- Tier gratuito: **20 chamadas/minuto**, até **30.000 rostos cadastrados** no total.
- Risco real: várias assinaturas em sequência rápida (ex.: todo mundo assinando presença do DDS matinal ao mesmo tempo) pode bater o limite de taxa — implementar retry com backoff curto.
- Migração para tier pago (S0) é só troca de chave de API, sem mudança de código.
- Chaves via configuração (mesmo padrão de outros segredos do projeto — `appsettings`/variável de ambiente, nunca hardcoded).

## 8. Fora de escopo (não mexer nesta rodada)

- ASO — sem fluxo de assinatura hoje; não entra nesta feature.
- "Ordem de Serviço" (OS) — documento que ainda não existe no sistema; feature futura separada, mencionada pelo usuário mas não especificada aqui.
- Matching facial local/offline de verdade (ex.: face-api.js rodando no navegador) — avaliado e descartado; o usuário optou por "foto pendente, verificada depois" em vez de uma segunda tecnologia de matching.
- Substituir o Futronic — reconhecimento facial é aditivo, Futronic continua valendo nos postos fixos que já têm o leitor.

## 9. Testes

- Unitário: `IAutenticacaoFacialService` com fake do cliente Azure — thresholds (aceita ≥85%, rejeita 60-85% com mensagem específica, rejeita <60%/sem rosto, bloqueia múltiplos rostos).
- Unitário: fluxo de enrollment — cria `Person`, associa foto, dispara `Train`, persiste `AzureFacePersonId`.
- Unitário: resolução de erro de rede cai no caminho de fila offline, não no caminho de "rosto não reconhecido".
- Frontend: fila de fotos pendentes (IndexedDB) grava offline e sincroniza quando volta a conexão — mesmo padrão de teste já usado pelo motor de sincronização existente.
