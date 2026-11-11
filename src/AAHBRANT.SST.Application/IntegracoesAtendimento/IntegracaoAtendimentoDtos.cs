namespace AAHBRANT.SST.Application.IntegracoesAtendimento;

// Contrato JSON (camelCase) de api/integracoes-atendimento. Enums saem como texto camelCase.
// Contrato completo: docs/2026-11-11-realizar-integracao-contrato.md.

public record AtendimentoEmAndamentoResumoDto(Guid Id, string Numero, string Status, int? EtapaAtualOrdem);

public record BloqueioIntegracaoDto(string Codigo, string Mensagem, string? Rota = null);

public record CandidatoIntegracaoDto(
    Guid TrabalhadorId, string Nome, Guid FuncaoId, string Funcao, Guid? PlanoIntegracaoId, bool TemPlanoAberto, bool JaParticipa,
    // Prévia das 5 etapas desta pessoa (mesma calculadora do Iniciar): nestaIntegracao | valido | naoExigido.
    IReadOnlyList<EtapaCandidatoDto> Etapas);

public record InstrutorIntegracaoDto(Guid TrabalhadorId, string Nome, string? Registro);

public record EntradaIntegracaoDto(
    bool Habilitada, Guid ObraId, string ObraNome, AtendimentoEmAndamentoResumoDto? AtendimentoEmAndamento, bool PodeIniciar,
    IReadOnlyList<BloqueioIntegracaoDto> Bloqueios, IReadOnlyList<CandidatoIntegracaoDto> Candidatos,
    IReadOnlyList<InstrutorIntegracaoDto> Instrutores);

public record ParticipanteIntegracaoDto(
    Guid TrabalhadorId, string Nome, Guid FuncaoId, string Funcao, Guid? PlanoIntegracaoId, Guid? OrdemServicoId);

public record PeriodoEtapaIntegracaoDto(Guid Id, string Data, string Inicio, string Fim, int Minutos);

public record ParticipanteEtapaIntegracaoDto(
    Guid TrabalhadorId, string Nome, bool PresencaConfirmada, DateTime? PresencaEmUtc, string? Metodo, bool TemDigital, bool TemFacial,
    bool NaoAssinou = false, string? MotivoNaoAssinou = null);

public record EtapaIntegracaoDto(
    Guid Id, int Ordem, string Tipo, string Titulo, string Status, Guid? CursoTreinamentoId, Guid? SessaoTreinamentoId,
    int CargaMinimaHoras, int CargaLancadaMinutos, DateTime? IniciadaEmUtc, DateTime? ConcluidaEmUtc,
    int FotosRegistradas, int FotosNecessarias,
    IReadOnlyList<ParticipanteEtapaIntegracaoDto> Participantes,
    IReadOnlyList<PeriodoEtapaIntegracaoDto> Periodos,
    // Só na etapa em andamento: o que ainda impede o botão "Avançar".
    IReadOnlyList<string> Faltas,
    // Conteúdo programático do curso e para quem vale (null na OS).
    string? Conteudo = null, string? Aplicabilidade = null);

public record EtapaCandidatoDto(string Tipo, int Ordem, string Situacao);

public record IntegracaoAtendimentoDto(
    Guid Id, string Numero, Guid ObraId, string ObraNome, string Status, int? EtapaAtualOrdem,
    DateTime IniciadaEmUtc, DateTime? ConcluidaEmUtc, Guid? ResponsavelUsuarioId, string? ResponsavelNome,
    Guid InstrutorTrabalhadorId, string? InstrutorNome, int Versao,
    // Pessoas podem entrar/sair enquanto a primeira etapa de curso não tiver nenhuma presença assinada (Q5).
    bool PodeEditarParticipantes,
    IReadOnlyList<ParticipanteIntegracaoDto> Participantes, IReadOnlyList<EtapaIntegracaoDto> Etapas);

// OS por pessoa na última etapa.
public record PendenciaAgrupadaOsDto(string Tipo, string Titulo, string QuemResolve, IReadOnlyList<Guid> TrabalhadoresIds, IReadOnlyList<string> Nomes);

public record LinhaOsIntegracaoDto(
    Guid TrabalhadorId, string Nome, string Funcao,
    // "naoEmitida" | "aguardandoFuncionario" | "aguardandoTecnico" | "assinada" | "pendente" (há pendência e nada emitido/assinado nesta integração).
    string Situacao, Guid? OrdemServicoId, string? Numero, DateTime? AssinadaEmUtc,
    bool PodeEmitir, IReadOnlyList<string> Pendencias);

public record OsIntegracaoDto(
    Guid IntegracaoId, bool EtapaOsEmAndamento, int TotalPessoas, int Assinadas, int Pendentes, bool PodeConcluir,
    IReadOnlyList<LinhaOsIntegracaoDto> Pessoas, IReadOnlyList<PendenciaAgrupadaOsDto> PendenciasAgrupadas);

public record TurmaIntegracaoAgendadaDto(
    Guid SessaoTreinamentoId, Guid ObraId, string ObraNome, string CursoNome, DateTime Data, string Status, int Participantes);

public static class TextoDto
{
    public static string Camel(string nome) => string.IsNullOrEmpty(nome) ? nome : char.ToLowerInvariant(nome[0]) + nome[1..];
}

// Listagem para Treinamentos › Integração.
public record IntegracaoResumoDto(
    Guid Id, string Numero, Guid ObraId, string ObraNome, string Status, int? EtapaAtualOrdem, string? EtapaAtualTitulo,
    int Participantes, DateTime IniciadaEmUtc, DateTime? ConcluidaEmUtc, string? ResponsavelNome);
