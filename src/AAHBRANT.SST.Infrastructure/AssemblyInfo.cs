using System.Runtime.CompilerServices;

// Expõe os tipos/membros internal de Infrastructure (ex.: CalendarioTeamsMensagemHandler,
// GraphCalendarioTeamsService.MontarEventoDiaInteiro) para teste direto — sem isso, os testes
// precisariam de reflection ou duplicar a lógica. Repositório não usa mocking library nenhuma
// (ver tests/**/*.csproj), então essa é a única forma limpa de testar lógica internal.
[assembly: InternalsVisibleTo("AAHBRANT.SST.Application.Tests")]
