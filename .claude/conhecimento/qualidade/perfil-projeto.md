# Perfil do projeto: AHBT-SST (Hub Gênesis SST)

Levantado em 2026-11-12 a partir do código. Serve de mapa para os agentes de qualidade (segurança, desempenho, carga, padronização, testes). O que vale é o código atual: se este arquivo divergir do repositório, confie no repositório e avise que o perfil precisa de atualização.

## Stack

- **Backend:** .NET, solução `SST-APP.sln`, em camadas: `AAHBRANT.SST.Domain`, `.Application`, `.Infrastructure`, `.Api` e `.Worker`. Migrations com EF Core (o CI usa `dotnet-ef` 8.0.11 e falha se houver migration pendente).
- **Frontend:** `src/AAHBRANT.SST.TeamsApp`, React 19 + Vite 8, aplicativo do Microsoft Teams (`@microsoft/teams-js`), login com `@azure/msal-browser`, PWA (`vite-plugin-pwa`) com armazenamento offline em IndexedDB via Dexie.
- **Outros projetos na mesma solução:** `AAHBRANT.SST.AgenteBiometria` (agente instalado na máquina, com leitores biométricos, endpoints locais, ícone de bandeja e atualização automática), seu instalador, e `AAHBRANT.JURI.Api` (API separada).
- **Infraestrutura:** `Dockerfile.api`, `Dockerfile.web`, `Dockerfile.worker`, `nginx.conf`, GitHub Actions em `.github/workflows/ci.yml` e `deploy.yml`. Destino Azure, conforme `PROJECT RULES.md`.
- **Testes:** xUnit em `tests/` (Domain, Application, Infrastructure, integração da Api, AgenteBiometria) e Playwright no TeamsApp, com configurações separadas para DDS, offline, PWA, cronograma, uniforme e inspeção offline.

## Autenticação e autorização

- JWT do Microsoft Entra ID via `AddMicrosoftIdentityWebApi`, configurado em `src/AAHBRANT.SST.Api/Program.cs`.
- Autorização por permissão em `src/AAHBRANT.SST.Api/Autorizacao/` (`PermissaoAuthorizationHandler`, `PermissaoAuthorizationPolicyProvider`, `PoliticasAutorizacao`, `AppRolesReconhecidas`, `UsuarioAtualResolver`).
- Middlewares em `src/AAHBRANT.SST.Api/Middlewares/`: `EscopoPorObraMiddleware` (isolamento por obra), `VinculoAzureAdMiddleware`, `IdempotenciaMiddleware`, `TratamentoDeExcecaoMiddleware`.
- São 93 controllers. Na data do levantamento, `[AllowAnonymous]` aparecia em três: `DispositivosAgenteController`, `IdentificacaoPublicaController` e `ValidacaoPublicaController`.
- Limite de requisições por IP com duas políticas: `agente-anonimo` (30 por minuto) e `publico` (120 por minuto).
- CORS com política de desenvolvimento e política de produção com origem configurada; Swagger habilitado condicionalmente em `Program.cs`.

## Dados sensíveis

- **Biometria** dos trabalhadores (agente de biometria e identificação).
- **Dados de saúde ocupacional** (ASO, exames).
- **Assinaturas** de documentos e de presença em DDS.
- **Imagens** de câmeras para verificação de EPI e fotos de inspeção.
- **Dados pessoais** de trabalhadores, inclusive os recebidos de integração com RH.
- O que fica gravado no navegador para uso offline (IndexedDB) merece atenção própria.

Biometria e saúde são dados pessoais sensíveis pela LGPD. Os agentes apontam risco técnico; a avaliação de conformidade cabe ao jurídico ou ao encarregado de dados.

## Integrações (seções de `appsettings.json`)

`AzureAd`, `Graph` (Microsoft Graph), `ServiceBus`, `AzureOpenAI`, `SuporteIa`, `Telegram`, `Grh` (RH), `Cameras`, `Assinatura`, `Lgpd`, `Cors`.

## Pontos que já deram problema

- Assinatura de documento e presença de DDS com vários tablets ao mesmo tempo geravam registros duplicados (corrigido no PR #339 e na migration seguinte). Concorrência em operações que precisam ser únicas é área de atenção permanente, tanto para segurança quanto para testes de carga.

## A confirmar com a equipe

- Se existe ambiente de homologação e qual o endereço. Enquanto não houver, teste ativo e teste de carga rodam só em ambiente local.
- Metas de desempenho (tempo de resposta aceitável e usuários simultâneos esperados).
- Banco de dados usado em produção e onde ficam os arquivos (Blob Storage ou outro).
