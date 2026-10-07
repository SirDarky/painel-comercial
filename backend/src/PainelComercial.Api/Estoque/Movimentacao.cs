using System.ComponentModel.DataAnnotations;

namespace PainelComercial.Api.Estoque;

public sealed class Movimentacao
{
    /// <summary>Número único da movimentação, gerado pelo banco (autoincremento).</summary>
    public long Id { get; set; }
    public int CodigoProduto { get; set; }
    public Produto Produto { get; set; } = null!;
    public TipoMovimentacao Tipo { get; set; }
    public int Quantidade { get; set; }
    public required string Descricao { get; set; }
    public DateTimeOffset DataHora { get; set; }
    public int EstoqueAnterior { get; set; }
    public int EstoqueFinal { get; set; }

    /// <summary>Valor do cabeçalho Idempotency-Key, para não lançar duas vezes a mesma movimentação.</summary>
    public string? ChaveIdempotencia { get; set; }
}

public sealed record MovimentacaoResponse(
    long Id,
    int CodigoProduto,
    string DescricaoProduto,
    TipoMovimentacao Tipo,
    int Quantidade,
    string Descricao,
    DateTimeOffset DataHora,
    int EstoqueAnterior,
    int EstoqueFinal);

public sealed record NovaMovimentacaoRequest(
    [Required(ErrorMessage = "Informe o código do produto.")]
    int? CodigoProduto,

    [Required(ErrorMessage = "Informe o tipo da movimentação: Entrada ou Saida.")]
    [EnumDataType(typeof(TipoMovimentacao), ErrorMessage = "Tipo de movimentação inválido. Use Entrada ou Saida.")]
    TipoMovimentacao? Tipo,

    [Required(ErrorMessage = "Informe a quantidade.")]
    [Range(1, 1_000_000, ErrorMessage = "A quantidade deve estar entre 1 e 1.000.000.")]
    int? Quantidade,

    [Required(ErrorMessage = "Informe a descrição da movimentação.")]
    [StringLength(200, ErrorMessage = "A descrição deve ter no máximo 200 caracteres.")]
    string? Descricao);
