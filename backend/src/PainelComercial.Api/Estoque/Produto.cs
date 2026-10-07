namespace PainelComercial.Api.Estoque;

public enum TipoMovimentacao
{
    Entrada = 1,
    Saida = 2
}

public sealed class Produto
{
    public int CodigoProduto { get; set; }
    public required string DescricaoProduto { get; set; }
    public int Estoque { get; set; }
}
