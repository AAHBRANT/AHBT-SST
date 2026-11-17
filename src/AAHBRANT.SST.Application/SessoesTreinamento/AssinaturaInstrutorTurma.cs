using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Assinatura.Commands;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento;

// O instrutor já assinou esta turma/etapa: a API responde 409.
public sealed class AssinaturaInstrutorJaRegistradaException(string mensagem) : InvalidOperationException(mensagem);

public record AssinaturaInstrutorTurmaDto(Guid SessaoTreinamentoId, Guid InstrutorTrabalhadorId, string Metodo, DateTime AssinadoEmUtc);

public record DadosAssinaturaInstrutorDigital(
    Guid DispositivoId, string SegredoDispositivo, Guid TrabalhadorId, double Score,
    string? Desafio = null, string? AssinaturaLeitura = null, byte[]? ImagemDigital = null, LocalizacaoAssinatura? Localizacao = null);

public record DadosAssinaturaInstrutorFacial(byte[] FotoJpeg, string? ContentType = null, LocalizacaoAssinatura? Localizacao = null);

// Assinatura biométrica do instrutor ANTES do encerramento da turma (decisão do usuário, 16/11/2026): digital ou facial, uma por
// turma, guardada com a mesma prova que a presença do participante guarda. No encerramento (EncerrarSessaoTreinamentoCommand)
// ela vira a assinatura do instrutor, papel Instrutor, em cada certificado. Sem biometria não há assinatura pelo login.
// Compartilhado entre a turma comum (SessoesTreinamentoController) e a etapa da integração (IntegracoesAtendimentoController).
public static class AssinaturaInstrutorTurma
{
    public static string Contexto(SessaoTreinamento sessao) => sessao.IntegracaoAtendimentoId is null ? "turma" : "integração";

    // Turma ainda aberta, com instrutor definido e sem assinatura dele. Filtros globais: turma de outra obra = 404.
    public static async Task<SessaoTreinamento> CarregarParaAssinarAsync(IAppDbContext db, Guid sessaoId, CancellationToken ct)
    {
        var sessao = await db.SessoesTreinamento.FirstOrDefaultAsync(s => s.Id == sessaoId, ct)
            ?? throw new KeyNotFoundException("Turma não encontrada.");
        var contexto = Contexto(sessao);
        if (sessao.Status == StatusSessaoTreinamento.Concluida)
            throw new InvalidOperationException($"Esta {contexto} já foi encerrada: a assinatura do instrutor é colhida antes do encerramento.");
        if (sessao.InstrutorTrabalhadorId is null)
            throw new InvalidOperationException($"Escolha o instrutor da {contexto} antes de assinar.");
        if (await db.AssinaturasInstrutorSessaoTreinamento.AnyAsync(a => a.SessaoTreinamentoId == sessao.Id, ct))
            throw new AssinaturaInstrutorJaRegistradaException($"O instrutor já assinou esta {contexto}.");
        return sessao;
    }

    public static async Task<AssinaturaInstrutorTurmaDto> PorDigitalAsync(
        IAppDbContext db, IAutenticacaoBiometriaLocalService autenticacao, IClienteIpProvider? clienteIp,
        SessaoTreinamento sessao, DadosAssinaturaInstrutorDigital dados, CancellationToken ct)
    {
        var instrutorId = sessao.InstrutorTrabalhadorId!.Value;
        // A digital lida precisa ser a do instrutor escolhido: sem isso, qualquer perfil de instrutor assinaria no lugar de outro.
        if (dados.TrabalhadorId != instrutorId)
            throw new InvalidOperationException($"A digital lida não é a do instrutor desta {Contexto(sessao)}.");
        await GarantirPerfilInstrutorAsync(db, instrutorId, ct);

        var resultado = await autenticacao.AutenticarPorMatchLocalAsync(
            dados.DispositivoId, dados.SegredoDispositivo, dados.TrabalhadorId, dados.Score,
            string.IsNullOrEmpty(dados.Desafio) ? null : new LeituraBiometricaAssinada(dados.Desafio, dados.AssinaturaLeitura ?? string.Empty),
            ct, permitirInstrutorDeOutraObra: true);
        if (resultado.TrabalhadorId != instrutorId)
            throw new InvalidOperationException($"A digital lida não é a do instrutor desta {Contexto(sessao)}.");

        return await GravarAsync(db, clienteIp, sessao, resultado, dados.Score, dados.ImagemDigital, "image/png", dados.Localizacao, ct);
    }

    public static async Task<AssinaturaInstrutorTurmaDto> PorFacialAsync(
        IAppDbContext db, IAutenticacaoFacialService facial, IClienteIpProvider? clienteIp,
        SessaoTreinamento sessao, DadosAssinaturaInstrutorFacial dados, CancellationToken ct)
    {
        var instrutorId = sessao.InstrutorTrabalhadorId!.Value;
        await GarantirPerfilInstrutorAsync(db, instrutorId, ct);

        // O rosto do instrutor está no grupo facial da obra DELE (a do cadastro), não no da obra da turma.
        var obraDoInstrutor = await db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => t.Id == instrutorId).Select(t => (Guid?)t.ObraId).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Instrutor não encontrado.");
        var identificacao = await facial.IdentificarAsync(obraDoInstrutor, dados.FotoJpeg, ct);
        if (!identificacao.Aceito)
            throw new RejeicaoFacialException(identificacao.Motivo!.Value, MensagemRejeicaoFacial.Para(identificacao.Motivo));
        if (identificacao.Resultado!.TrabalhadorId != instrutorId)
            throw new InvalidOperationException($"A face lida não é a do instrutor desta {Contexto(sessao)}.");

        return await GravarAsync(db, clienteIp, sessao, identificacao.Resultado, identificacao.Confianca, dados.FotoJpeg, dados.ContentType, dados.Localizacao, ct);
    }

    private static async Task GarantirPerfilInstrutorAsync(IAppDbContext db, Guid instrutorId, CancellationToken ct)
    {
        if (!await db.PerfisInstrutor.AnyAsync(p => p.TrabalhadorId == instrutorId, ct))
            throw new InvalidOperationException("Este trabalhador não tem perfil de instrutor cadastrado.");
    }

    private static async Task<AssinaturaInstrutorTurmaDto> GravarAsync(
        IAppDbContext db, IClienteIpProvider? clienteIp, SessaoTreinamento sessao, ResultadoAutenticacaoAssinatura resultado,
        double? confianca, byte[]? imagem, string? contentType, LocalizacaoAssinatura? localizacao, CancellationToken ct)
    {
        if (resultado.Metodo == MetodoAutenticacaoAssinatura.SessaoLogada)
            throw new InvalidOperationException("O instrutor assina por digital ou reconhecimento facial, não pela sessão logada.");

        var assinatura = new AssinaturaInstrutorSessaoTreinamento
        {
            ObraId = sessao.ObraId,
            SessaoTreinamentoId = sessao.Id,
            InstrutorTrabalhadorId = resultado.TrabalhadorId,
            Metodo = resultado.Metodo,
            AssinadoEmUtc = DateTime.UtcNow,
            ScoreConfianca = confianca,
        };
        EvidenciaPresencaTreinamento.AplicarNaAssinaturaInstrutor(assinatura, resultado, imagem, contentType, localizacao, clienteIp);
        db.AssinaturasInstrutorSessaoTreinamento.Add(assinatura);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Corrida: outra chamada gravou antes (índice único por turma).
            if (await db.AssinaturasInstrutorSessaoTreinamento.IgnoreQueryFilters()
                    .AnyAsync(a => a.SessaoTreinamentoId == sessao.Id && a.Ativo && a.Id != assinatura.Id, ct))
                throw new AssinaturaInstrutorJaRegistradaException($"O instrutor já assinou esta {Contexto(sessao)}.");
            throw;
        }
        return new AssinaturaInstrutorTurmaDto(sessao.Id, assinatura.InstrutorTrabalhadorId, TextoMetodo(assinatura.Metodo), assinatura.AssinadoEmUtc);
    }

    public static string TextoMetodo(MetodoAutenticacaoAssinatura metodo)
    {
        var nome = metodo.ToString();
        return char.ToLowerInvariant(nome[0]) + nome[1..];
    }
}
