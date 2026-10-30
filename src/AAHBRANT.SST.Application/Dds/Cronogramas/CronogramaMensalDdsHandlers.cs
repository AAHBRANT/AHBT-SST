using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Cronogramas;

public record ObterCronogramaMensalDdsQuery(Guid ObraId, int Ano, int Mes) : IRequest<CronogramaMensalDdsDto>;
public record SalvarCronogramaMensalDdsCommand(
    Guid ObraId, int Ano, int Mes, int Versao, List<DiaCronogramaDdsEntrada> Dias, string? AzureAdObjectId)
    : IRequest<CronogramaMensalDdsDto>;

public class ObterCronogramaMensalDdsQueryHandler(IAppDbContext db)
    : IRequestHandler<ObterCronogramaMensalDdsQuery, CronogramaMensalDdsDto>
{
    public async Task<CronogramaMensalDdsDto> Handle(ObterCronogramaMensalDdsQuery request, CancellationToken ct)
    {
        ConteudoCronogramaDds.ValidarCompetencia(request.Ano, request.Mes);
        if (!await db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException("Obra não encontrada ou sem acesso.");
        var registro = await db.CronogramasMensaisDds.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ObraId == request.ObraId && c.Ano == request.Ano && c.Mes == request.Mes, ct);
        if (registro is null)
            return new(null, request.ObraId, request.Ano, request.Mes, 0, null, [], []);
        var revisoes = await db.CronogramasMensaisDdsRevisoes.AsNoTracking()
            .Where(r => r.CronogramaId == registro.Id && r.DocumentoAssinaturaId != null)
            .OrderByDescending(r => r.Numero)
            .Select(r => new RevisaoCronogramaDdsDto(r.Id, r.Numero, r.VersaoOrigem, r.CreatedAtUtc))
            .ToListAsync(ct);
        return new(registro.Id, registro.ObraId, registro.Ano, registro.Mes, registro.Versao,
            registro.NumeroDocumento, ConteudoCronogramaDds.Ler(registro.ProgramacaoJson), revisoes);
    }
}

public class SalvarCronogramaMensalDdsCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService numeros)
    : IRequestHandler<SalvarCronogramaMensalDdsCommand, CronogramaMensalDdsDto>
{
    public async Task<CronogramaMensalDdsDto> Handle(SalvarCronogramaMensalDdsCommand request, CancellationToken ct)
    {
        ConteudoCronogramaDds.ValidarCompetencia(request.Ano, request.Mes);
        if (!await db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException("Obra não encontrada ou sem acesso.");
        var usuario = string.IsNullOrWhiteSpace(request.AzureAdObjectId) ? null :
            await db.Usuarios.FirstOrDefaultAsync(u => u.AzureAdObjectId == request.AzureAdObjectId, ct);
        if (usuario is null) throw new InvalidOperationException("Seu usuário não está vinculado a um cadastro reconhecido.");
        if (request.Versao < 0 || request.Dias is null || request.Dias.Count > DateTime.DaysInMonth(request.Ano, request.Mes)
            || request.Dias.Any(d => d is null || d.Data.Year != request.Ano || d.Data.Month != request.Mes)
            || request.Dias.Select(d => d.Data).Distinct().Count() != request.Dias.Count
            || request.Dias.Any(d => d.TemasIds is null || d.TemasIds.Count > 50
                || d.TemasIds.Any(id => id == Guid.Empty)
                || d.TemasIds.Distinct().Count() != d.TemasIds.Count))
            throw new InvalidOperationException("Revise as datas e os temas: use dias deste mês, sem duplicação e até 50 temas por dia.");

        var registro = await db.CronogramasMensaisDds
            .FirstOrDefaultAsync(c => c.ObraId == request.ObraId && c.Ano == request.Ano && c.Mes == request.Mes, ct);
        if ((registro?.Versao ?? 0) != request.Versao)
            throw new DbUpdateConcurrencyException("O cronograma foi alterado. Recarregue antes de salvar.");
        var antigos = registro is null ? [] : ConteudoCronogramaDds.Ler(registro.ProgramacaoJson);
        var ids = request.Dias.SelectMany(d => d.TemasIds).Distinct().ToList();
        var catalogo = await db.CatalogosTemaDds.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, ct);
        var dias = new List<DiaCronogramaDdsDto>();
        foreach (var dia in request.Dias.OrderBy(d => d.Data))
        {
            var temas = new List<TemaCronogramaDdsDto>();
            foreach (var id in dia.TemasIds)
            {
                // Mantém a cópia já escolhida naquele dia, inclusive se o tema for desativado.
                // Uma nova escolha precisa existir no catálogo ativo.
                var anterior = antigos.FirstOrDefault(d => d.Data == dia.Data)?.Temas.FirstOrDefault(t => t.CatalogoTemaDdsId == id);
                if (anterior is not null) temas.Add(anterior);
                else if (catalogo.TryGetValue(id, out var tema)) temas.Add(new(id, tema.Nome, tema.Descricao));
                else throw new InvalidOperationException("Um tema selecionado não está mais disponível. Reabra a seleção e revise o dia.");
            }
            if (temas.Count > 0) dias.Add(new(dia.Data, temas));
        }
        var json = JsonSerializer.Serialize(dias);
        if (registro is null)
        {
            registro = new CronogramaMensalDds
            {
                ObraId = request.ObraId, Ano = request.Ano, Mes = request.Mes,
                ResponsavelUsuarioId = usuario.Id, CreatedBy = usuario.Id,
                NumeroDocumento = await numeros.GerarAsync("DDS-CRON", ct), ProgramacaoJson = json,
            };
            db.CronogramasMensaisDds.Add(registro);
        }
        else if (registro.ProgramacaoJson != json)
        {
            registro.ProgramacaoJson = json;
            registro.Versao++;
            registro.UpdatedBy = usuario.Id;
        }
        await db.SaveChangesAsync(ct);
        return await new ObterCronogramaMensalDdsQueryHandler(db)
            .Handle(new(request.ObraId, request.Ano, request.Mes), ct);
    }
}
