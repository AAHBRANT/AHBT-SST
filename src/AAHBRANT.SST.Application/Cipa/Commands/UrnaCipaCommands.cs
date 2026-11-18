using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

// Urna biométrica da CIPA (16/11/2026). Fluxo no tablet do canteiro:
//   1. Técnico abre a votação (AbrirVotacaoCipaCommand) — congela o nº de eleitores aptos.
//   2. Eleitor encosta o dedo / olha a câmera → Identificar*Command confere quem é (mesmos serviços de
//      biometria do DDS/treinamento), se é empregado CLT ativo da obra e se ainda não votou, e devolve
//      uma CÉDULA (Id aleatório do EleitorCipa, válida por ValidadeCedula).
//   3. Eleitor toca na foto e confirma → VotarCipaCommand grava o comprovante (VotouEmUtc) e, em
//      tabela separada e sem vínculo, o voto (VotoCipa). Ver disclosure de sigilo em Cipa.cs.
//   4. Técnico encerra (EncerrarVotacaoCipaCommand) → confere quórum de 50% (NR-5) e conta os votos
//      (status VotacaoRealizada). A classificação titulares/suplentes e o mandato seguem pela apuração
//      já existente (RegistrarApuracaoProcessoEleitoralCipaCommand), que usa a contagem da urna.
// Regras da NR-5 citadas aqui (quórum de 50%, voto secreto) devem ser validadas pelo técnico de SST.
public static class UrnaCipa
{
    public static readonly TimeSpan ValidadeCedula = TimeSpan.FromMinutes(3);

    public static IQueryable<Trabalhador> EleitoresAptos(IAppDbContext db, Guid obraId) =>
        db.Trabalhadores.Where(t => t.ObraId == obraId && t.DataDemissao == null && t.Vinculo == TipoVinculo.Clt);

    public static async Task<ProcessoEleitoralCipa> ObterProcessoComVotacaoAbertaAsync(IAppDbContext db, Guid processoId, CancellationToken ct)
    {
        var processo = await db.ProcessosEleitoraisCipa.FirstOrDefaultAsync(p => p.Id == processoId, ct)
            ?? throw new KeyNotFoundException("Processo eleitoral não encontrado.");
        if (processo.Status != StatusProcessoEleitoralCipa.VotacaoAberta)
            throw new InvalidOperationException("A votação desta eleição não está aberta.");
        return processo;
    }

    // Emite (ou renova) a cédula do eleitor. Lança JaVotouCipaException se ele já votou.
    public static async Task<CedulaCipaDto> EmitirCedulaAsync(IAppDbContext db, ProcessoEleitoralCipa processo, Guid trabalhadorId,
        MetodoIdentificacaoEleitorCipa metodo, Guid? liberadoPor, CancellationToken ct)
    {
        var trabalhador = await EleitoresAptos(db, processo.ObraId)
            .Where(t => t.Id == trabalhadorId)
            .Select(t => new { t.Id, t.Nome, Funcao = t.Funcao!.Nome })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Esta pessoa não é empregado ativo desta obra e não pode votar nesta eleição.");

        var eleitor = await db.EleitoresCipa
            .FirstOrDefaultAsync(e => e.ProcessoEleitoralId == processo.Id && e.TrabalhadorId == trabalhadorId, ct);

        if (eleitor?.VotouEmUtc is { } votouEm)
            throw new JaVotouCipaException(trabalhador.Nome, votouEm);

        var agora = DateTime.UtcNow;
        if (eleitor is null)
        {
            eleitor = new EleitorCipa { ProcessoEleitoralId = processo.Id, TrabalhadorId = trabalhadorId };
            db.EleitoresCipa.Add(eleitor);
        }
        eleitor.IdentificadoEmUtc = agora;
        eleitor.Metodo = metodo;
        eleitor.LiberadoPorUsuarioId = liberadoPor;
        await db.SaveChangesAsync(ct);

        return new CedulaCipaDto(eleitor.Id, trabalhador.Nome, trabalhador.Funcao, agora.Add(ValidadeCedula));
    }
}

public record CedulaCipaDto(Guid CedulaId, string EleitorNome, string EleitorFuncao, DateTime ExpiraEmUtc);

public class JaVotouCipaException : InvalidOperationException
{
    public JaVotouCipaException(string nome, DateTime votouEmUtc)
        : base($"{nome} já votou nesta eleição.")
    {
        Nome = nome;
        VotouEmUtc = votouEmUtc;
    }

    public string Nome { get; }
    public DateTime VotouEmUtc { get; }
}

// ---------- 1. Abrir votação ----------
public record AbrirVotacaoCipaCommand(Guid ProcessoEleitoralId) : IRequest<int>;

public class AbrirVotacaoCipaCommandHandler : IRequestHandler<AbrirVotacaoCipaCommand, int>
{
    private readonly IAppDbContext _db;
    public AbrirVotacaoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(AbrirVotacaoCipaCommand request, CancellationToken ct)
    {
        var processo = await _db.ProcessosEleitoraisCipa.FirstOrDefaultAsync(p => p.Id == request.ProcessoEleitoralId, ct)
            ?? throw new KeyNotFoundException("Processo eleitoral não encontrado.");

        if (processo.Status is StatusProcessoEleitoralCipa.VotacaoAberta)
            throw new InvalidOperationException("A votação já está aberta.");
        if (processo.Status is StatusProcessoEleitoralCipa.VotacaoRealizada or StatusProcessoEleitoralCipa.Apurado or StatusProcessoEleitoralCipa.Encerrado)
            throw new InvalidOperationException("Esta eleição já teve a votação encerrada.");

        var candidatos = await _db.CandidatosCipa.CountAsync(c => c.ProcessoEleitoralId == processo.Id && c.Status == StatusCandidatoCipa.Deferido, ct);
        if (candidatos == 0)
            throw new InvalidOperationException("Aprove pelo menos um candidato antes de abrir a votação.");

        processo.TotalEleitoresAptos = await UrnaCipa.EleitoresAptos(_db, processo.ObraId).CountAsync(ct);
        processo.VotacaoAbertaEmUtc = DateTime.UtcNow;
        processo.VotacaoEncerradaEmUtc = null;
        processo.Status = StatusProcessoEleitoralCipa.VotacaoAberta;
        await _db.SaveChangesAsync(ct);
        return processo.TotalEleitoresAptos.Value;
    }
}

// ---------- 2a. Identificar pela digital ----------
public record IdentificarEleitorDigitalCipaCommand(
    Guid ProcessoEleitoralId,
    Guid TrabalhadorId,
    Guid DispositivoId,
    string SegredoDispositivo,
    double Score,
    string? Desafio = null,
    string? AssinaturaLeitura = null) : IRequest<CedulaCipaDto>;

public class IdentificarEleitorDigitalCipaCommandHandler : IRequestHandler<IdentificarEleitorDigitalCipaCommand, CedulaCipaDto>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;

    public IdentificarEleitorDigitalCipaCommandHandler(IAppDbContext db, IAutenticacaoBiometriaLocalService autenticacao)
    {
        _db = db;
        _autenticacao = autenticacao;
    }

    public async Task<CedulaCipaDto> Handle(IdentificarEleitorDigitalCipaCommand request, CancellationToken ct)
    {
        var processo = await UrnaCipa.ObterProcessoComVotacaoAbertaAsync(_db, request.ProcessoEleitoralId, ct);

        // Confere no servidor que a leitura veio de um leitor cadastrado e casou com este trabalhador
        // (mesmo mecanismo da presença em treinamento) — o navegador não consegue forjar a identidade.
        await _autenticacao.AutenticarPorMatchLocalAsync(
            request.DispositivoId, request.SegredoDispositivo, request.TrabalhadorId, request.Score,
            string.IsNullOrEmpty(request.Desafio) ? null : new LeituraBiometricaAssinada(request.Desafio, request.AssinaturaLeitura ?? string.Empty), ct);

        return await UrnaCipa.EmitirCedulaAsync(_db, processo, request.TrabalhadorId, MetodoIdentificacaoEleitorCipa.Digital, null, ct);
    }
}

// ---------- 2b. Identificar pelo rosto ----------
public record IdentificarEleitorFacialCipaCommand(Guid ProcessoEleitoralId, byte[] FotoJpeg) : IRequest<CedulaCipaDto>;

public class IdentificarEleitorFacialCipaCommandHandler : IRequestHandler<IdentificarEleitorFacialCipaCommand, CedulaCipaDto>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _facial;

    public IdentificarEleitorFacialCipaCommandHandler(IAppDbContext db, IAutenticacaoFacialService facial)
    {
        _db = db;
        _facial = facial;
    }

    public async Task<CedulaCipaDto> Handle(IdentificarEleitorFacialCipaCommand request, CancellationToken ct)
    {
        var processo = await UrnaCipa.ObterProcessoComVotacaoAbertaAsync(_db, request.ProcessoEleitoralId, ct);
        var identificacao = await _facial.IdentificarAsync(processo.ObraId, request.FotoJpeg, ct);
        if (!identificacao.Aceito)
            throw new RejeicaoFacialException(identificacao.Motivo!.Value, MensagemRejeicaoFacial.Para(identificacao.Motivo));

        return await UrnaCipa.EmitirCedulaAsync(_db, processo, identificacao.Resultado!.TrabalhadorId, MetodoIdentificacaoEleitorCipa.Facial, null, ct);
    }
}

// ---------- 2c. Mesário libera (biometria falhou) ----------
public record LiberarEleitorMesarioCipaCommand(Guid ProcessoEleitoralId, Guid TrabalhadorId) : IRequest<CedulaCipaDto>;

public class LiberarEleitorMesarioCipaCommandHandler : IRequestHandler<LiberarEleitorMesarioCipaCommand, CedulaCipaDto>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _usuario;

    public LiberarEleitorMesarioCipaCommandHandler(IAppDbContext db, ICurrentUserService usuario)
    {
        _db = db;
        _usuario = usuario;
    }

    public async Task<CedulaCipaDto> Handle(LiberarEleitorMesarioCipaCommand request, CancellationToken ct)
    {
        var processo = await UrnaCipa.ObterProcessoComVotacaoAbertaAsync(_db, request.ProcessoEleitoralId, ct);
        return await UrnaCipa.EmitirCedulaAsync(_db, processo, request.TrabalhadorId, MetodoIdentificacaoEleitorCipa.Mesario, _usuario.UsuarioId, ct);
    }
}

// ---------- 3. Votar ----------
public record VotarCipaCommand(Guid ProcessoEleitoralId, Guid CedulaId, Guid CandidatoCipaId) : IRequest<int>;

public class VotarCipaCommandHandler : IRequestHandler<VotarCipaCommand, int>
{
    private readonly IAppDbContext _db;
    public VotarCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<int> Handle(VotarCipaCommand request, CancellationToken ct)
    {
        var processo = await UrnaCipa.ObterProcessoComVotacaoAbertaAsync(_db, request.ProcessoEleitoralId, ct);

        var eleitor = await _db.EleitoresCipa
            .FirstOrDefaultAsync(e => e.Id == request.CedulaId && e.ProcessoEleitoralId == processo.Id, ct)
            ?? throw new InvalidOperationException("Cédula inválida. Identifique-se de novo.");
        if (eleitor.VotouEmUtc is not null)
            throw new InvalidOperationException("Este eleitor já votou.");
        if (DateTime.UtcNow - eleitor.IdentificadoEmUtc > UrnaCipa.ValidadeCedula)
            throw new InvalidOperationException("O tempo para votar acabou. Identifique-se de novo.");

        var candidatoValido = await _db.CandidatosCipa.AnyAsync(c =>
            c.Id == request.CandidatoCipaId && c.ProcessoEleitoralId == processo.Id && c.Status == StatusCandidatoCipa.Deferido, ct);
        if (!candidatoValido)
            throw new InvalidOperationException("Candidato inválido para esta eleição.");

        // Minuto cheio: suficiente para o "já votou às 09:14" e reduz a precisão do horário guardado.
        var agora = DateTime.UtcNow;
        eleitor.VotouEmUtc = new DateTime(agora.Year, agora.Month, agora.Day, agora.Hour, agora.Minute, 0, DateTimeKind.Utc);
        _db.VotosCipa.Add(new VotoCipa { Id = Guid.NewGuid(), ProcessoEleitoralId = processo.Id, CandidatoCipaId = request.CandidatoCipaId });
        await _db.SaveChangesAsync(ct); // comprovante e voto na mesma transação: ou os dois, ou nenhum

        return await _db.EleitoresCipa.CountAsync(e => e.ProcessoEleitoralId == processo.Id && e.VotouEmUtc != null, ct);
    }
}

// ---------- 4. Encerrar e apurar ----------
public record EncerrarVotacaoCipaCommand(Guid ProcessoEleitoralId) : IRequest<ResultadoEncerramentoCipaDto>;

public record ResultadoEncerramentoCipaDto(bool Apurado, int Votantes, int Aptos, string Mensagem);

public class EncerrarVotacaoCipaCommandHandler : IRequestHandler<EncerrarVotacaoCipaCommand, ResultadoEncerramentoCipaDto>
{
    private readonly IAppDbContext _db;
    public EncerrarVotacaoCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ResultadoEncerramentoCipaDto> Handle(EncerrarVotacaoCipaCommand request, CancellationToken ct)
    {
        var processo = await UrnaCipa.ObterProcessoComVotacaoAbertaAsync(_db, request.ProcessoEleitoralId, ct);
        var aptos = processo.TotalEleitoresAptos ?? 0;
        var votantes = await _db.EleitoresCipa.CountAsync(e => e.ProcessoEleitoralId == processo.Id && e.VotouEmUtc != null, ct);

        // NR-5: com participação inferior a 50% dos empregados não há apuração — a votação continua aberta.
        if (!AtingiuQuorum(votantes, aptos))
            return new ResultadoEncerramentoCipaDto(false, votantes, aptos,
                $"Participação de {votantes} de {aptos} (menos de 50%). Pela NR-5 não há apuração: mantenha a votação aberta ou prorrogue.");

        var contagem = await _db.VotosCipa
            .Where(v => v.ProcessoEleitoralId == processo.Id)
            .GroupBy(v => v.CandidatoCipaId)
            .Select(g => new { CandidatoId = g.Key, Total = g.Count() })
            .ToListAsync(ct);

        var candidatos = await _db.CandidatosCipa
            .Where(c => c.ProcessoEleitoralId == processo.Id && c.Status == StatusCandidatoCipa.Deferido)
            .ToListAsync(ct);
        foreach (var c in candidatos)
            c.VotosRecebidos = contagem.FirstOrDefault(x => x.CandidatoId == c.Id)?.Total ?? 0;

        processo.VotacaoEncerradaEmUtc = DateTime.UtcNow;
        processo.Status = StatusProcessoEleitoralCipa.VotacaoRealizada;
        await _db.SaveChangesAsync(ct);

        return new ResultadoEncerramentoCipaDto(true, votantes, aptos,
            "Votação encerrada e votos contados. Informe o mandato para definir titulares e suplentes.");
    }

    public static bool AtingiuQuorum(int votantes, int aptos) => aptos > 0 && votantes * 2 >= aptos;
}
