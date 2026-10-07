using System.ComponentModel.DataAnnotations;
using PainelComercial.Api.Comum;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PainelComercial.Api.Estoque;

[ApiController]
[Route("api/estoque")]
public sealed class EstoqueController(EstoqueService estoque) : ControllerBase
{
    /// <summary>Produtos com o estoque atual.</summary>
    [HttpGet("produtos")]
    public Task<List<Produto>> ListarProdutos(CancellationToken cancellationToken) =>
        estoque.ListarProdutosAsync(cancellationToken);

    /// <summary>Histórico de movimentações (mais recentes primeiro).</summary>
    [HttpGet("movimentacoes")]
    public Task<List<MovimentacaoResponse>> ListarMovimentacoes(
        [FromQuery] int? codigoProduto, CancellationToken cancellationToken) =>
        estoque.ListarMovimentacoesAsync(codigoProduto, cancellationToken);

    [HttpGet("movimentacoes/{id:long}")]
    [ProducesResponseType<MovimentacaoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimentacaoResponse>> ObterMovimentacao(long id, CancellationToken cancellationToken)
    {
        var movimentacao = await estoque.ObterMovimentacaoAsync(id, cancellationToken);
        return movimentacao is null ? NotFound() : movimentacao;
    }

    /// <summary>Lança uma entrada ou saída de mercadoria e devolve o estoque final do produto.</summary>
    /// <remarks>
    /// Envie o cabeçalho opcional <c>Idempotency-Key</c> (ex.: um UUID) para que repetir a mesma
    /// requisição devolva a movimentação já lançada em vez de lançar outra.
    /// </remarks>
    [HttpPost("movimentacoes")]
    [EnableRateLimiting(LimiteDeRequisicoes.Escrita)]
    [ProducesResponseType<MovimentacaoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<MovimentacaoResponse>> Lancar(
        NovaMovimentacaoRequest request,
        [FromHeader(Name = "Idempotency-Key")]
        [StringLength(100, ErrorMessage = "O cabeçalho Idempotency-Key deve ter no máximo 100 caracteres.")]
        string? chaveIdempotencia,
        CancellationToken cancellationToken)
    {
        // Os campos obrigatórios já foram validados pelo [ApiController] (400 se faltar algum).
        var movimentacao = await estoque.MovimentarAsync(
            request.CodigoProduto!.Value,
            request.Tipo!.Value,
            request.Quantidade!.Value,
            request.Descricao!,
            string.IsNullOrWhiteSpace(chaveIdempotencia) ? null : chaveIdempotencia.Trim(),
            cancellationToken);

        return CreatedAtAction(nameof(ObterMovimentacao), new { id = movimentacao.Id }, movimentacao);
    }
}
