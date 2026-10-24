# Fotos com data, hora e geolocalização — G-SST

Status: diretrizes de negócio aprovadas pelo usuário em 28/09/2026; primeira implementação dos fluxos de evidência, inspeção e acidentes concluída em 28/09/2026.

## 1. Decisões aprovadas

- Todas as fotos utilizadas no G-SST devem ter data, hora e local da captura, com geolocalização vinculada à imagem.
- O módulo de acidentes deve manter a descrição do local e exigir **três fotos do local do acidente**, em três espaços no formulário.
- Cada foto deve ter seus próprios dados de captura. A data e a hora do acidente continuam em campos separados: o acidente e a fotografia podem ocorrer em momentos diferentes.
- Verificar a localização ao abrir a câmera e apresentar o estado ao usuário: obtida, aguardando ou permissão bloqueada, com possibilidade de tentar novamente. Tratar também indisponibilidade e precisão insuficiente.
- Se a localização falhar, preservar o preenchimento como rascunho e manter a pendência. O registro não deve ser considerado completo enquanto os requisitos fotográficos não forem atendidos.
- A regularização pode exigir nova captura. Não atribuir coordenadas obtidas posteriormente a uma foto antiga como se fossem a localização original.
- Não preencher retroativamente fotos existentes com data, hora ou coordenadas atuais como se fossem dados de captura.

O usuário aprovou seguir a orientação apresentada na conversa. A regra de rascunho com pendência está aprovada; não foi aprovada uma alternativa de liberação mediante justificativa.

## 2. Comportamento previsto

1. Ao abrir a câmera, solicitar a localização e apresentar o resultado ou a orientação necessária para liberar a permissão.
2. Associar cada captura a uma leitura de localização suficientemente recente, registrando a precisão informada pelo provedor quando disponível.
3. Preservar a imagem original e os dados estruturados. Produzir uma versão com carimbo de data, hora e local para visualização, download e relatórios.
4. Validar os requisitos no servidor, além dos avisos na interface.
5. Preservar o histórico quando uma foto for substituída. Uma nova foto recebe novos dados de captura.

Para acidentes, sugerir os enquadramentos: visão geral do ambiente, ponto da ocorrência e detalhe relevante. São orientações de preenchimento, sem reconhecimento automático do conteúdo nesta proposta.

Dados previstos por foto:

| Informação | Finalidade |
| --- | --- |
| Data/hora de captura e fuso | Identificar quando a foto foi produzida |
| Data/hora de recebimento no servidor | Separar captura de envio |
| Latitude e longitude | Registrar a posição fornecida pelo dispositivo |
| Momento da leitura de localização e precisão | Avaliar a atualidade e a qualidade da posição |
| Obra/área e descrição do local | Dar contexto operacional às coordenadas |
| Usuário responsável e origem da imagem | Identificar autoria do registro e captura direta ou importação |
| Estado da evidência e motivo da pendência | Impedir que uma captura incompleta seja tratada como completa |
| Identificador, hash e vínculo ao registro | Apoiar integridade e histórico; o hash não comprova a veracidade da localização |

## 3. Situação encontrada no código

- A captura compartilhada está em `src/AAHBRANT.SST.TeamsApp/src/components/camera/useCapturaFoto.ts`, usada por `SeletorFotoCamera` e pelos componentes de fotos. Hoje o retorno é um arquivo, sem contrato comum de metadados geográficos.
- `src/AAHBRANT.SST.TeamsApp/src/lib/imagem.ts` recria imagens em JPEG por canvas. Eventuais metadados de arquivos importados precisam ser lidos antes dessa transformação e preservados separadamente.
- Há fotos armazenadas diretamente como bytes em diferentes entidades, como DDS, treinamentos, inspeções, trabalhadores e catálogos. Alterar apenas a câmera não atende à regra em todo o sistema.
- `Evidencia` já possui latitude e longitude opcionais, mas esses campos não constituem um fluxo uniforme para os caminhos de foto observados.
- O manifesto do Teams atualmente declara `devicePermissions: ["media"]`; a integração de localização requer sua configuração própria.
- `Acidente` possui `Local`, `Data` e `Hora`, mas não possui coleção própria de fotos. Seu fluxo atual é Registrado → Em investigação → Concluído. A criação não tem um estado de rascunho e já publica o evento para o G-RH; a implementação deverá acomodar a nova pendência sem publicar um rascunho como registro completo.

## 4. Sequência de implementação planejada

1. **Inventário e contrato comum:** mapear todas as entradas, substituições, visualizações e exportações de fotos, inclusive assinaturas e anexos de imagem. Definir dados comuns e a representação das pendências em cada módulo.
2. **Validação nos dispositivos:** integrar câmera e localização no navegador e no Teams, em celular e computador. Validar permissões, indisponibilidade, demora e precisão. Usar resultados reais para definir limites de precisão, idade da leitura e tempo de espera.
3. **Persistência e validação:** transportar imagem e metadados, preservar o original, registrar o recebimento no servidor, controlar histórico e impedir conclusão com pendências no backend.
4. **Acidentes:** adicionar três espaços de foto, contagem de preenchimento e validação de completude. Adequar rascunho, evolução de status e integração com o G-RH ao fluxo aprovado.
5. **Expansão:** aplicar o mesmo padrão a DDS, treinamentos, inspeções/alojamentos, trabalhadores, catálogos e evidências de assinatura; atualizar visualizações e relatórios. Carimbos devem ser gerados em cópias, preservando a imagem usada em processamento facial.
6. **Histórico e arquivos importados:** identificar fotos sem dados originais como legado ou pendentes, conforme a política de migração definida. Metadados de arquivo importado têm origem declarada e não devem ser tratados automaticamente como equivalentes à captura direta.

A primeira entrega aplica a validação a novas fotos de DDS, treinamentos, inspeções e acidentes. Fotos legadas sem JSON de metadados são preservadas e continuam utilizáveis; novas substituições passam a exigir o contrato completo. Os demais cadastros de foto seguem o mesmo contrato na etapa de expansão.

A persistência de rascunho no servidor não implica suporte offline. Armazenamento local e sincronização sem conexão precisam ser dimensionados separadamente, caso necessários.

## 5. Verificação de aceite

- Cada foto nova regular contém seus próprios dados de captura e localização, preservados após compressão e envio.
- As três fotos do local do acidente são exigidas; uma ou duas fotos não tornam o registro completo.
- Localização negada, indisponível, demorada ou inadequada preserva o rascunho e produz uma pendência compreensível.
- Requisições diretas à API não conseguem contornar as exigências de completude.
- A data do acidente, a data da foto e o recebimento no servidor permanecem distintos.
- Substituição de imagem não reutiliza os metadados da foto anterior.
- Relatórios e downloads apresentam os dados correspondentes à foto exibida; o original permanece preservado.
- Fotos antigas não ganham dados de captura inventados. A migração não reabre ou invalida silenciosamente registros já concluídos.
- Validar câmera, localização e continuidade de preenchimento no Teams e no navegador, em celular e computador.

## 6. Detalhes a fechar durante o desenho técnico

As decisões acima não fixam valores de precisão ou tempo de espera, critérios de aceitação de fotos importadas, armazenamento dos originais nem o tratamento de cada registro legado. Esses detalhes devem ser definidos explicitamente antes de ativar as respectivas validações.

O módulo de acidentes também reúne outros tipos de ocorrência. A exigência aprovada é para acidentes; verificar a aplicabilidade aos demais tipos ao detalhar o formulário, especialmente ocorrências sem um local físico fotografável.

## 7. Referências consultadas

- [Microsoft — Permissões de dispositivo no Teams](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/device-capabilities/native-device-permissions): permissões e uso da API de localização no cliente desktop.
- [Microsoft — Integração de localização](https://learn.microsoft.com/en-us/microsoftteams/platform/concepts/device-capabilities/location-capability): manifesto, capacidades e erros. Usar localização atual, sem permitir escolher livremente outro ponto como posição da captura.
- [W3C — Geolocation](https://www.w3.org/TR/geolocation/): posição, precisão, timestamp e falhas por permissão, indisponibilidade e timeout.
- [GPS.gov — Precisão do GPS](https://www.gps.gov/gps-accuracy): limitações de recepção em ambientes internos e junto a estruturas.
