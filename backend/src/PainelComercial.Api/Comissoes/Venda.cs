using System.ComponentModel.DataAnnotations;

namespace PainelComercial.Api.Comissoes;

public sealed record Venda(
    [Required(ErrorMessage = "Informe o nome do vendedor.")]
    [StringLength(100, ErrorMessage = "O nome do vendedor deve ter no máximo 100 caracteres.")]
    string Vendedor,

    [Range(0.01, 1_000_000_000, ErrorMessage = "O valor da venda deve estar entre R$ 0,01 e R$ 1.000.000.000,00.")]
    decimal Valor);

/// <summary>Mesmo formato do arquivo vendas.json: <c>{ "vendas": [ ... ] }</c>.</summary>
public sealed record ListaVendas(
    [Required(ErrorMessage = "Informe a lista de vendas.")]
    [MinLength(1, ErrorMessage = "Informe ao menos uma venda.")]
    [MaxLength(10_000, ErrorMessage = "Envie no máximo 10.000 vendas por cálculo.")]
    IReadOnlyList<Venda> Vendas);
