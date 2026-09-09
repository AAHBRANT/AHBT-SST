using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;

namespace AAHBRANT.SST.Application.Trabalhadores.Commands;

// Carga inicial única do cadastro de colaboradores do G-RH (Integração G-RH, contrato acordado em
// 2026-09-09 — GET /api/integracoes/sst/colaboradores, sem paginação nem filtro incremental,
// "roda uma vez, importa tudo"). A atualização contínua depois disso é por evento via Service Bus
// (ServiceBusColaboradorGrhProcessor), não por repetir esta carga.
//
// Cada colaborador é sincronizado individualmente via SincronizarColaboradorGrhCommand (mesma lógica
// de upsert por CPF do consumidor de eventos) — uma falha isolada (ex.: Obra ainda não cadastrada no
// SST com o nome informado) não aborta o lote inteiro, só entra no relatório de erros.
public record ImportarColaboradoresGrhCommand : IRequest<ImportarColaboradoresGrhResultado>;

public record ImportarColaboradoresGrhResultado(int TotalRecebidos, int TotalSincronizados, IReadOnlyList<string> Erros);

public class ImportarColaboradoresGrhCommandHandler : IRequestHandler<ImportarColaboradoresGrhCommand, ImportarColaboradoresGrhResultado>
{
    private readonly IColaboradorGrhClient _client;
    private readonly IMediator _mediator;

    public ImportarColaboradoresGrhCommandHandler(IColaboradorGrhClient client, IMediator mediator)
    {
        _client = client;
        _mediator = mediator;
    }

    public async Task<ImportarColaboradoresGrhResultado> Handle(ImportarColaboradoresGrhCommand request, CancellationToken ct)
    {
        var colaboradores = await _client.ListarTodosAsync(ct);
        var erros = new List<string>();
        var totalSincronizados = 0;

        foreach (var c in colaboradores)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(c.CargoNome))
                    throw new InvalidOperationException("colaborador sem cargo definido no G-RH.");

                await _mediator.Send(
                    new SincronizarColaboradorGrhCommand(
                        c.Cpf,
                        c.Nome,
                        c.Pis,
                        c.Ctps,
                        c.DataNascimento,
                        c.NomeMae,
                        c.Endereco,
                        c.Municipio,
                        c.Uf,
                        c.Cep,
                        c.Matricula,
                        c.DataAdmissao,
                        c.DataDemissao,
                        c.CargoNome,
                        c.CargoCboCodigo,
                        c.Salario,
                        c.Situacao,
                        c.ObraNome,
                        c.DataFimExperiencia1,
                        c.DataFimExperiencia2,
                        c.TamanhoBlusaEpi,
                        c.TamanhoCalcaEpi,
                        c.TamanhoCalcadoEpi),
                    ct);
                totalSincronizados++;
            }
            catch (Exception ex)
            {
                erros.Add($"CPF {CpfMascarador.Mascarar(c.Cpf)} ({c.Nome}): {ex.Message}");
            }
        }

        return new ImportarColaboradoresGrhResultado(colaboradores.Count, totalSincronizados, erros);
    }
}
