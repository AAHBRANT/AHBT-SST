using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ListarDdsSemanaisQuery(Guid? ObraId = null) : IRequest<List<DdsSemanalDto>>;

public class ListarDdsSemanaisQueryHandler : IRequestHandler<ListarDdsSemanaisQuery, List<DdsSemanalDto>>
{
    private readonly IAppDbContext _db;

    public ListarDdsSemanaisQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<DdsSemanalDto>> Handle(ListarDdsSemanaisQuery request, CancellationToken ct)
    {
        var query = _db.DdsSemanais
            .Include(s => s.Obra)
            .Include(s => s.ResponsavelUsuario)
            .Include(s => s.ResponsavelObraSstUsuario)
            .Include(s => s.RegistrosDiarios)
            .AsQueryable();

        if (request.ObraId.HasValue)
            query = query.Where(s => s.ObraId == request.ObraId.Value);

        var lista = await query.OrderByDescending(s => s.DataInicioSemana).ToListAsync(ct);

        var dtos = lista.Select(MapearParaDto).ToList();
        var pessoas = await FotosPessoas.DeUsuariosAsync(_db, dtos.Select(d => (Guid?)d.ResponsavelUsuarioId), ct);
        foreach (var dto in dtos)
        {
            if (!pessoas.TryGetValue(dto.ResponsavelUsuarioId, out var p)) continue;
            dto.ResponsavelTrabalhadorId = p.TrabalhadorId;
            dto.ResponsavelTemFoto = p.TemFoto;
        }
        return dtos;
    }

    internal static DdsSemanalDto MapearParaDto(Domain.Entidades.DdsSemanal semanal)
    {
        var dias = semanal.RegistrosDiarios.Where(d => d.Ativo).ToList();
        return new DdsSemanalDto
        {
            Id = semanal.Id,
            ObraId = semanal.ObraId,
            ObraNome = semanal.Obra?.Nome ?? string.Empty,
            Tipo = semanal.Tipo,
            EmpresaTerceirizada = semanal.EmpresaTerceirizada,
            NumeroDocumento = semanal.NumeroDocumento,
            LocalFrenteServico = semanal.LocalFrenteServico,
            ResponsavelUsuarioId = semanal.ResponsavelUsuarioId,
            ResponsavelUsuarioNome = semanal.ResponsavelUsuario?.Nome ?? string.Empty,
            DataInicioSemana = semanal.DataInicioSemana,
            DataFimSemana = semanal.DataFimSemana,
            Status = semanal.Status,
            ResponsavelObraSstNome = semanal.ResponsavelObraSstUsuario?.Nome,
            ResponsavelEmpresaTerceirizadaNome = semanal.ResponsavelEmpresaTerceirizadaNome,
            ResponsavelEmpresaTerceirizadaFuncao = semanal.ResponsavelEmpresaTerceirizadaFuncao,
            EncerradaEm = semanal.EncerradaEm,
            TotalDiasRegistrados = dias.Count,
            TotalDiasConcluidos = dias.Count(d => d.Status == StatusDds.Concluido),
            DiasResumo = dias.OrderBy(d => d.Data).Select(d => new DdsSemanalDiaResumoDto(d.Data, d.Status == StatusDds.Concluido)).ToList(),
        };
    }
}
