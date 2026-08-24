# Matriz RBAC — App de SST AAHBRANT

> **Status: rascunho técnico de partida.** Precisa de validação da Diretoria/Gestor QSMS antes de
> virar regra de autorização definitiva em produção (pendência já registrada no plano aprovado).
> Baseado nos enums reais em `src/AAHBRANT.SST.Domain/Enums/Enums.cs`.

## 1. Modelo de escopo

Escopo é resolvido **por usuário × obra**, não é fixo por perfil — a tabela `UsuarioPerfilObra`
permite que o mesmo usuário tenha perfis diferentes em obras diferentes (ex.: Gestor de Obra na
Obra A, Encarregado na Obra B).

| Sigla | Escopo (`EscopoAcesso`) | Significado |
|---|---|---|
| **G** | Global | Toda a empresa, todas as unidades/obras |
| **U** | Unidade | Todas as obras de uma ou mais unidades vinculadas |
| **O** | Obra | Apenas a(s) obra(s) vinculada(s) ao usuário |
| **P** | Proprio | Apenas os próprios dados (ex.: trabalhador vendo seu próprio ASO) |

Ações: **V**er · **C**riar · **E**ditar · **A**provar (muda status oficial) · **X**cluir (sempre soft-delete via `Ativo=false`).

## 2. Perfis (`TipoPerfilAcesso`)

Os 12 perfis confirmados em código: `Administrador, Diretor, GestorQsms, EngenheiroSeguranca,
TecnicoSeguranca, MedicoDoTrabalho, Rh, GestorDeObra, Encarregado, Trabalhador, Auditor, Terceiro`.

## 3. Matriz por módulo

Escopo-padrão de cada perfil (pode ser refinado por atribuição individual em `UsuarioPerfilObra`):

| Perfil | Escopo típico |
|---|---|
| Administrador | G (acesso técnico/configuração, **não** implica acesso irrestrito a dado clínico) |
| Diretor | G |
| GestorQsms | G ou U |
| EngenheiroSeguranca | U ou O |
| TecnicoSeguranca | O |
| MedicoDoTrabalho | G (dado clínico) / O (operacional) |
| Rh | U ou O |
| GestorDeObra | O |
| Encarregado | O (equipe/setor dentro da obra) |
| Trabalhador | P |
| Auditor | G ou U, **somente leitura** |
| Terceiro | O, restrito ao próprio contrato — regra específica pendente de validação jurídica/QSMS |

### Empresa / Unidade / Obra / Setor / Equipe / Função (cadastro organizacional)

| Perfil | V | C | E | X |
|---|---|---|---|---|
| Administrador | G | G | G | G |
| Diretor | G | — | — | — |
| GestorQsms | U | U | U | — |
| EngenheiroSeguranca | O | — | — | — |
| TecnicoSeguranca | O | — | — | — |
| GestorDeObra | O | — | O (dados operacionais da própria obra) | — |
| Encarregado | O | — | — | — |
| Trabalhador | P (só o que lhe diz respeito) | — | — | — |
| Auditor | G/U | — | — | — |
| Terceiro | O (limitado ao contrato) | — | — | — |

### Trabalhador (cadastro)

| Perfil | V | C | E | X |
|---|---|---|---|---|
| Administrador | G | G | G | G |
| GestorQsms | U | U | U | — |
| Rh | U/O | O | O | — |
| EngenheiroSeguranca | O | — | — | — |
| GestorDeObra | O | — | — | — |
| Encarregado | O (equipe) | — | — | — |
| Trabalhador | P (próprio cadastro) | — | — | — |
| Auditor | G/U | — | — | — |

### ASO (`Aso`, `AsoRestricao`) — módulo mais restritivo (dado de saúde sensível)

**Regra central:** apenas **Médico do Trabalho** vê o conteúdo clínico completo
(`TipoExameAso`, `ResultadoAso` detalhado, restrições) e homologa o resultado. Todos os demais
perfis (exceto o próprio Trabalhador) veem **somente o status** ("Apto" / "Apto com restrição" /
"Inapto" / "Pendente"), nunca o detalhe clínico.

| Perfil | Vê status | Vê clínico completo | Cria/Edita | Aprova (homologa resultado) |
|---|---|---|---|---|
| MedicoDoTrabalho | Sim | **Sim (único)** | Sim | **Sim (único)** |
| Trabalhador | Sim (próprio) | Sim (próprio) | — | — |
| Administrador | Sim | **Não por padrão** | Não | Não |
| Diretor | Sim | Não | — | — |
| GestorQsms | Sim | Não | — | — |
| EngenheiroSeguranca | Sim | Não | — | — |
| TecnicoSeguranca | Sim | Não | — | — |
| Rh | Sim (agendamento/controle) | Não | Cria agendamento | Não |
| GestorDeObra | Sim | Não | — | — |
| Encarregado | Sim | Não | — | — |
| Auditor | Sim | Não | — | — |
| Terceiro | Sim (próprio, se aplicável) | Não | — | — |

Todo acesso ao registro clínico (não só alteração) deve gravar em `TrilhaAuditoria`
(`Acao = "Aso.VisualizarClinico"`), conforme já modelado.

### Treinamento / EPI (`Treinamento`, `EntregaEpi`)

| Perfil | V | C | E | A |
|---|---|---|---|---|
| GestorQsms | U | U | U | — |
| EngenheiroSeguranca | O | O | O | — |
| TecnicoSeguranca | O | O | O | — |
| Rh | U/O | O | O | — |
| GestorDeObra | O | — | — | — |
| Encarregado | O (equipe) | O (registro de entrega EPI) | — | — |
| Trabalhador | P | — | — | — |

### Liberação de atividade de risco / motor de elegibilidade (§45)

**Regra crítica:** a aprovação/liberação de atividade de risco (ex.: aprovar Permissão de Trabalho
quando o motor de elegibilidade acusa bloqueio) **nunca** fica disponível isoladamente para
Encarregado, Gestor de Obra ou Trabalhador.

| Perfil | Pode aprovar liberação de atividade de risco |
|---|---|
| EngenheiroSeguranca | **Sim** |
| TecnicoSeguranca | Somente sob delegação formal — **a confirmar com QSMS** |
| GestorQsms | Sim (nível gerencial) |
| Todos os demais | Não |

### Alerta (`Alerta`, `AlertaHistoricoEnvio`)

| Perfil | V | Trata/Fecha | Recebe escalonamento |
|---|---|---|---|
| GestorQsms | U | Sim | Sim (destino final do escalonamento automático) |
| EngenheiroSeguranca | O | Sim | Sim |
| TecnicoSeguranca | O | Sim | — |
| GestorDeObra | O | Parcial (operacional) | — |
| Encarregado | O | — | — |
| Trabalhador | P | — | — |

### Auditoria / Evidência (`TrilhaAuditoria`, `Evidencia`)

| Perfil | Vê trilha de auditoria | Vê evidências |
|---|---|---|
| Auditor | **Sim (G/U, somente leitura)** | Sim |
| GestorQsms | Sim (U) | Sim |
| Administrador | Sim (técnico, para fins de suporte) | Sim |
| Demais perfis | Não (exceto evidências do próprio módulo/obra que já acessam) | Conforme módulo de origem |

Nenhum perfil tem UPDATE/DELETE sobre `TrilhaAuditoria` — é append-only mesmo para Administrador,
inclusive a nível de permissão de banco (fora do escopo da aplicação: revisar permissões de role
SQL quando o banco de produção for provisionado).

## 4. Implementação técnica (3 camadas, conforme plano aprovado)

1. **Policy-based por perfil**: `[Authorize(Policy = "Aso.HomologarResultado")]` nos endpoints de
   ação sensível, mapeando perfil → policy em `Program.cs`/`AddAuthorization`.
2. **Handler de escopo por obra**: um `IAuthorizationHandler` que consulta `UsuarioPerfilObra` para
   confirmar que o usuário tem o perfil exigido **naquela obra específica** (não apenas em alguma
   obra qualquer).
3. **Global Query Filter (EF Core)**: filtro de escopo aplicado a nível de `DbContext` (além do
   `HasQueryFilter(x => x.Ativo)` já existente) para que nenhum endpoint dependa de lembrar de
   filtrar manualmente por obra — mitiga BOLA (Broken Object Level Authorization).

## 5. Pendências que exigem validação da Diretoria/Gestor QSMS (bloqueante antes de produção)

- Confirmar se Técnico de Segurança pode homologar liberação de atividade de risco sob delegação
  formal, e em quais condições.
- Confirmar se Administrador deve ter alguma via de acesso emergencial ao dado clínico (ex.:
  auditoria de incidente grave) e, se sim, sob qual controle adicional (aprovação dupla, alerta
  automático ao Médico do Trabalho e à Diretoria).
- Definir regras específicas de escopo e retenção de dados para o perfil **Terceiro** por contrato
  (o quanto ele vê de trabalhadores/obras que não são dele).
- Confirmar se Rh precisa de visão de status de ASO por unidade inteira ou apenas por obra.
