using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Inspecoes.Commands;

// Regra de 03/11/2026: ao clicar em "Finalizar inspeção", quem realizou a inspeção assina na hora, em um
// clique (sessão logada), e a inspeção vira Concluída. Roda logo depois de EncerrarInspecaoCommand.
// Não bloqueia: se não der para assinar (usuário não é o responsável, sem vínculo com trabalhador, ou
// função que não é Técnico/Engenheiro de Segurança — regra da assinatura de inspeção), a inspeção fica
// em "Aguardando assinatura" para um Técnico/Engenheiro assinar pela tela de assinatura.
public record AssinarInspecaoAoFinalizarCommand(Guid InspecaoId, string? AzureAdObjectId) : IRequest<ResultadoAssinaturaAoFinalizar>;

public record ResultadoAssinaturaAoFinalizar(bool Assinada, string? AssinadaPor, string? Motivo);

public class AssinarInspecaoAoFinalizarCommandHandler : IRequestHandler<AssinarInspecaoAoFinalizarCommand, ResultadoAssinaturaAoFinalizar>
{
    private readonly IAppDbContext _db;
    private readonly IRegistradorAssinaturaService _registrador;

    public AssinarInspecaoAoFinalizarCommandHandler(IAppDbContext db, IRegistradorAssinaturaService registrador)
    {
        _db = db;
        _registrador = registrador;
    }

    public async Task<ResultadoAssinaturaAoFinalizar> Handle(AssinarInspecaoAoFinalizarCommand request, CancellationToken ct)
    {
        var inspecao = await _db.Inspecoes.FirstOrDefaultAsync(i => i.Id == request.InspecaoId, ct);
        if (inspecao is null || inspecao.Status != StatusInspecao.AguardandoAssinatura)
            return Nao("A inspeção não está aguardando assinatura.");

        var usuario = string.IsNullOrEmpty(request.AzureAdObjectId)
            ? null
            : await _db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);
        if (usuario is null)
            return Nao("Seu usuário não foi identificado para assinar.");
        if (usuario.Id != inspecao.ResponsavelUsuarioId)
            return Nao("Só quem realizou a inspeção assina automaticamente ao finalizar.");
        if (usuario.TrabalhadorId is null)
            return Nao("Seu usuário não está vinculado a um cadastro de trabalhador.");

        var trabalhador = await _db.Trabalhadores.Include(t => t.Funcao)
            .FirstOrDefaultAsync(t => t.Id == usuario.TrabalhadorId.Value, ct);
        if (trabalhador is null)
            return Nao("Seu cadastro de trabalhador não foi encontrado.");
        if (!FuncaoSstClassifier.EhTecnicoSeguranca(trabalhador.Funcao?.Nome) && !FuncaoSstClassifier.EhEngenheiroSeguranca(trabalhador.Funcao?.Nome))
            return Nao("Apenas Técnico de Segurança ou Engenheiro de Segurança podem assinar inspeções.");

        var documento = await _db.DocumentosAssinatura.FirstOrDefaultAsync(
            d => d.EntidadeTipo == nameof(Domain.Entidades.Inspecao) && d.EntidadeId == inspecao.Id, ct);
        if (documento is null)
            return Nao("O documento de assinatura da inspeção não foi encontrado.");

        try
        {
            var resultado = new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.SessaoLogada);
            await _registrador.RegistrarAsync(documento.Id, resultado, ipAddress: null, ct);
            return new ResultadoAssinaturaAoFinalizar(true, trabalhador.Nome, null);
        }
        catch (InvalidOperationException ex)
        {
            return Nao(ex.Message);
        }
    }

    private static ResultadoAssinaturaAoFinalizar Nao(string motivo) => new(false, null, motivo);
}
