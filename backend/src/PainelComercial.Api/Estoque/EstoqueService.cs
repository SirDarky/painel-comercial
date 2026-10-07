using System.Linq.Expressions;
using PainelComercial.Api.Comum;
using Microsoft.EntityFrameworkCore;

namespace PainelComercial.Api.Estoque;

public sealed class EstoqueService(EstoqueDbContext db, TimeProvider relogio)
{
    private static readonly Expression<Func<Movimentacao, MovimentacaoResponse>> ParaResposta = m =>
        new MovimentacaoResponse(
            m.Id,
            m.CodigoProduto,
            m.Produto.DescricaoProduto,
            m.Tipo,
            m.Quantidade,
            m.Descricao,
            m.DataHora,
            m.EstoqueAnterior,
            m.EstoqueFinal);

    public Task<List<Produto>> ListarProdutosAsync(CancellationToken cancellationToken = default) =>
        db.Produtos.AsNoTracking().OrderBy(p => p.CodigoProduto).ToListAsync(cancellationToken);

    /// <summary>Movimentações mais recentes primeiro, opcionalmente de um único produto.</summary>
    public Task<List<MovimentacaoResponse>> ListarMovimentacoesAsync(
        int? codigoProduto = null, CancellationToken cancellationToken = default)
    {
        var movimentacoes = db.Movimentacoes.AsNoTracking();
        if (codigoProduto is not null)
            movimentacoes = movimentacoes.Where(m => m.CodigoProduto == codigoProduto);

        return movimentacoes.OrderByDescending(m => m.Id).Select(ParaResposta).ToListAsync(cancellationToken);
    }

    public Task<MovimentacaoResponse?> ObterMovimentacaoAsync(long id, CancellationToken cancellationToken = default) =>
        db.Movimentacoes.AsNoTracking().Where(m => m.Id == id).Select(ParaResposta).SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Lança uma entrada ou saída e devolve a movimentação com o estoque final do produto.
    /// O saldo é alterado por um UPDATE condicional (saída: "WHERE Estoque >= quantidade") e a
    /// movimentação é gravada na mesma transação. Assim, saídas simultâneas nunca deixam o estoque
    /// negativo, mesmo com mais de uma instância da API usando o mesmo banco.
    /// Com <paramref name="chaveIdempotencia"/>, repetir a mesma requisição (duplo clique, nova
    /// tentativa após falha de rede) devolve a movimentação já lançada em vez de lançar outra.
    /// </summary>
    public async Task<MovimentacaoResponse> MovimentarAsync(
        int codigoProduto,
        TipoMovimentacao tipo,
        int quantidade,
        string descricao,
        string? chaveIdempotencia = null,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            throw new RegraNegocioException("A quantidade movimentada deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(descricao))
            throw new RegraNegocioException("Informe uma descrição para a movimentação.");
        if (!Enum.IsDefined(tipo))
            throw new RegraNegocioException("Tipo de movimentação inválido. Use Entrada ou Saida.");

        descricao = descricao.Trim();

        // No SQLite a transação já começa com bloqueio de escrita (BEGIN IMMEDIATE): duas requisições
        // com a mesma chave são processadas uma depois da outra, e a segunda encontra a primeira aqui.
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

        if (chaveIdempotencia is not null)
        {
            var jaLancada = await db.Movimentacoes.AsNoTracking()
                .Where(m => m.ChaveIdempotencia == chaveIdempotencia)
                .Select(ParaResposta)
                .SingleOrDefaultAsync(cancellationToken);

            if (jaLancada is not null)
            {
                var mesmaMovimentacao = jaLancada.CodigoProduto == codigoProduto && jaLancada.Tipo == tipo
                    && jaLancada.Quantidade == quantidade && jaLancada.Descricao == descricao;

                return mesmaMovimentacao
                    ? jaLancada
                    : throw new ConflitoException(
                        "Esta chave de idempotência já foi usada em outra movimentação. Gere uma chave nova.");
            }
        }

        var produtoMovimentado = db.Produtos.Where(p => p.CodigoProduto == codigoProduto);
        var limiteParaEntrada = int.MaxValue - quantidade;
        var atualizados = tipo == TipoMovimentacao.Entrada
            ? await produtoMovimentado
                .Where(p => p.Estoque <= limiteParaEntrada)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estoque, p => p.Estoque + quantidade), cancellationToken)
            : await produtoMovimentado
                .Where(p => p.Estoque >= quantidade)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estoque, p => p.Estoque - quantidade), cancellationToken);

        // Lido na mesma transação: já reflete o UPDATE acima e ninguém mais altera o produto até o commit.
        var produto = await produtoMovimentado.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new RecursoNaoEncontradoException($"Produto {codigoProduto} não encontrado.");

        if (atualizados == 0)
        {
            throw new RegraNegocioException(tipo == TipoMovimentacao.Saida
                ? $"Estoque insuficiente de \"{produto.DescricaoProduto}\": disponível {produto.Estoque}, solicitado {quantidade}."
                : $"A entrada ultrapassa o limite de estoque de \"{produto.DescricaoProduto}\".");
        }

        var movimentacao = new Movimentacao
        {
            CodigoProduto = codigoProduto,
            Tipo = tipo,
            Quantidade = quantidade,
            Descricao = descricao,
            DataHora = relogio.GetLocalNow(),
            EstoqueAnterior = tipo == TipoMovimentacao.Entrada ? produto.Estoque - quantidade : produto.Estoque + quantidade,
            EstoqueFinal = produto.Estoque,
            ChaveIdempotencia = chaveIdempotencia
        };

        db.Movimentacoes.Add(movimentacao);
        await db.SaveChangesAsync(cancellationToken);
        await transacao.CommitAsync(cancellationToken);

        return new MovimentacaoResponse(
            movimentacao.Id,
            produto.CodigoProduto,
            produto.DescricaoProduto,
            movimentacao.Tipo,
            movimentacao.Quantidade,
            movimentacao.Descricao,
            movimentacao.DataHora,
            movimentacao.EstoqueAnterior,
            movimentacao.EstoqueFinal);
    }
}
