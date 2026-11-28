/*
  Libera a assinatura do certificado dos participantes AUSENTES da turma CERT-2026-0023
  (NR-18, Obra Beira Rio, 10/11/2026), sem alterar código do sistema.

  O QUE FAZ (para cada participante ativo da turma sem presença e sem certificado):
    1. cria o registro em Treinamentos (certificado), copiando os dados da turma/curso;
    2. grava o vínculo em ParticipantesSessaoTreinamento.TreinamentoGeradoId;
    3. cria o DocumentosAssinatura "EmAndamento" (SEM nenhuma assinatura).
  Com isso o botão "Assinar" aparece na linha do participante e a assinatura é feita
  normalmente (digital, facial ou sessão logada do técnico) pela tela de quiosque.

  Prefixo do número: calculado pela norma do curso (NR-18 -> AAH-NR18).

  O QUE NÃO FAZ:
    - NÃO marca presença (PresencaConfirmadaEm continua nulo) e NÃO grava assinatura falsa.
      A lista de presença da turma continuará mostrando "Ausente" para essas pessoas.
    - NÃO mexe em quem já tem certificado.

  COMO USAR (SQL Server Management Studio / Azure Data Studio, no banco do ambiente da turma):
    1. Rode como está: ele só mostra o que seria feito e termina com ROLLBACK (nada é gravado).
    2. Confira os resultados. Se estiver certo, mude @Confirmar para 1 e rode de novo.
    3. Faça backup/snapshot do banco antes de rodar com @Confirmar = 1.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @NumeroTurma NVARCHAR(50) = N'CERT-2026-0023';
DECLARE @Confirmar   BIT          = 0;   -- 0 = só simula (ROLLBACK) | 1 = grava (COMMIT)

BEGIN TRAN;

-- 1) Turma: tem que existir exatamente uma.
DECLARE @SessaoId UNIQUEIDENTIFIER, @CursoId UNIQUEIDENTIFIER, @Validade INT;
DECLARE @Qtd INT = (SELECT COUNT(*) FROM SessoesTreinamento WHERE NumeroCertificado = @NumeroTurma);
IF @Qtd <> 1
BEGIN
    DECLARE @m NVARCHAR(200) = CONCAT(N'Esperava 1 turma com o número ', @NumeroTurma, N' e encontrei ', @Qtd, N'. Nada foi alterado.');
    THROW 50001, @m, 1;
END;

SELECT @SessaoId = s.Id,
       @CursoId  = s.CursoTreinamentoId,
       @Validade = CASE WHEN s.ValidadeEmMeses > 0 THEN s.ValidadeEmMeses ELSE c.ValidadeEmMeses END
FROM SessoesTreinamento s
JOIN CursosTreinamento c ON c.Id = s.CursoTreinamentoId
WHERE s.NumeroCertificado = @NumeroTurma;

-- 2) Prefixo dos certificados pela norma do curso, igual ao sistema (NR-18 -> AAH-NR18; sem NR -> AAH-CERT).
DECLARE @Norma NVARCHAR(100) = (SELECT NormaReferencia FROM CursosTreinamento WHERE Id = @CursoId);
DECLARE @Pos INT = PATINDEX(N'%[0-9]%', ISNULL(@Norma, N''));
DECLARE @Digitos NVARCHAR(10) = CASE WHEN @Pos > 0
    THEN LEFT(SUBSTRING(@Norma, @Pos, 10), PATINDEX(N'%[^0-9]%', SUBSTRING(@Norma, @Pos, 10) + N'x') - 1) END;
DECLARE @Prefixo NVARCHAR(50) = CASE WHEN @Digitos IS NULL THEN N'AAH-CERT'
    ELSE CONCAT(N'AAH-NR', RIGHT(CONCAT(N'0', @Digitos), 2)) END;

-- 3) Quem vai receber certificado (ausentes sem certificado).
SELECT p.Id AS ParticipanteId, p.TrabalhadorId, t.Nome,
       ROW_NUMBER() OVER (ORDER BY t.Nome) AS Ordem
INTO #Alvo
FROM ParticipantesSessaoTreinamento p
JOIN Trabalhadores t ON t.Id = p.TrabalhadorId
WHERE p.SessaoTreinamentoId = @SessaoId
  AND p.Ativo = 1
  AND p.PresencaConfirmadaEm IS NULL
  AND p.TreinamentoGeradoId IS NULL;

SELECT N'ANTES - participantes que receberão certificado' AS Etapa, Ordem, Nome FROM #Alvo ORDER BY Ordem;

DECLARE @Total INT = (SELECT COUNT(*) FROM #Alvo);
IF @Total = 0
    THROW 50003, N'Nenhum participante ausente sem certificado nesta turma. Nada foi alterado.', 1;

-- 4) Numeração: mesmo contador que o sistema usa (ContadoresDocumento), reservando @Total números.
DECLARE @Ano INT = YEAR(SYSUTCDATETIME());
DECLARE @Agora DATETIME2 = SYSUTCDATETIME();
DECLARE @Ultimo INT = (SELECT UltimoNumero FROM ContadoresDocumento WHERE Prefixo = @Prefixo AND Ano = @Ano);

IF @Ultimo IS NULL
BEGIN
    SET @Ultimo = 0;
    INSERT INTO ContadoresDocumento (Id, Prefixo, Ano, UltimoNumero, CreatedAtUtc, Origem, Ativo)
    VALUES (NEWID(), @Prefixo, @Ano, @Total, @Agora, 0, 1);
END
ELSE
    UPDATE ContadoresDocumento SET UltimoNumero = UltimoNumero + @Total, UpdatedAtUtc = @Agora
    WHERE Prefixo = @Prefixo AND Ano = @Ano;

-- 5) Certificados (Treinamentos) copiando os dados da turma, como o encerramento faz.
SELECT a.ParticipanteId, a.TrabalhadorId, NEWID() AS TreinamentoId,
       CONCAT(@Prefixo, N'-', @Ano, N'-', RIGHT(CONCAT(N'000', @Ultimo + a.Ordem), 4)) AS Numero
INTO #Novos
FROM #Alvo a;

INSERT INTO Treinamentos
    (Id, TrabalhadorId, CursoTreinamentoId, DataRealizacao, DataValidade, CargaHorariaRealizada,
     InstituicaoInstrutor, InstrutorRegistroProfissional, Local, ConteudoProgramatico,
     NumeroCertificado, SessaoTreinamentoId, OrigemCertificado, CreatedAtUtc, Origem, Ativo)
SELECT n.TreinamentoId, n.TrabalhadorId, s.CursoTreinamentoId, s.DataRealizacao,
       DATEADD(MONTH, @Validade, s.DataRealizacao), s.CargaHorariaRealizada,
       s.InstituicaoInstrutor, s.InstrutorRegistroProfissional, s.Local, s.ConteudoProgramatico,
       n.Numero, s.Id, 0, @Agora, 0, 1
FROM #Novos n
CROSS JOIN SessoesTreinamento s
WHERE s.Id = @SessaoId;

-- 6) Vínculo na turma.
UPDATE p SET p.TreinamentoGeradoId = n.TreinamentoId, p.UpdatedAtUtc = @Agora
FROM ParticipantesSessaoTreinamento p
JOIN #Novos n ON n.ParticipanteId = p.Id;

-- 7) Documento de assinatura em andamento (sem signatários).
INSERT INTO DocumentosAssinatura (Id, EntidadeTipo, EntidadeId, Status, CreatedAtUtc, Origem, Ativo)
SELECT NEWID(), N'Treinamento', n.TreinamentoId, 1, @Agora, 0, 1
FROM #Novos n
WHERE NOT EXISTS (SELECT 1 FROM DocumentosAssinatura d WHERE d.EntidadeTipo = N'Treinamento' AND d.EntidadeId = n.TreinamentoId);

-- 8) Conferência do resultado.
SELECT N'DEPOIS - certificados criados' AS Etapa, t.Nome, tr.NumeroCertificado, d.Status AS StatusDocumento,
       (SELECT COUNT(*) FROM DocumentoSignatarios s WHERE s.DocumentoAssinaturaId = d.Id) AS Assinaturas
FROM #Novos n
JOIN Trabalhadores t   ON t.Id = n.TrabalhadorId
JOIN Treinamentos tr   ON tr.Id = n.TreinamentoId
JOIN DocumentosAssinatura d ON d.EntidadeTipo = N'Treinamento' AND d.EntidadeId = n.TreinamentoId
ORDER BY t.Nome;

IF @Confirmar = 1
BEGIN
    COMMIT TRAN;
    SELECT N'GRAVADO (COMMIT).' AS Resultado;
END
ELSE
BEGIN
    ROLLBACK TRAN;
    SELECT N'SIMULAÇÃO: nada foi gravado (ROLLBACK). Mude @Confirmar para 1 para gravar.' AS Resultado;
END;

DROP TABLE IF EXISTS #Alvo;
DROP TABLE IF EXISTS #Novos;
