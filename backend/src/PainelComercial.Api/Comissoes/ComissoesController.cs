using PainelComercial.Api.Comum;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PainelComercial.Api.Comissoes;

[ApiController]
[Route("api/comissoes")]
public sealed class ComissoesController(ListaVendas vendasDoArquivo) : ControllerBase
{
    /// <summary>Comissão de cada vendedor, calculada a partir de Data/vendas.json.</summary>
    [HttpGet]
    public RelatorioComissoes Calcular() => CalculadoraComissao.Calcular(vendasDoArquivo.Vendas);

    /// <summary>Vendas lidas de Data/vendas.json.</summary>
    [HttpGet("vendas")]
    public ListaVendas ListarVendas() => vendasDoArquivo;

    /// <summary>Calcula a comissão para as vendas enviadas no corpo (mesmo formato do vendas.json).</summary>
    [HttpPost("calculo")]
    [EnableRateLimiting(LimiteDeRequisicoes.Escrita)]
    [ProducesResponseType<RelatorioComissoes>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    // [FromBody] explícito: ListaVendas também está registrada no DI, e sem ele o
    // [ApiController] injetaria as vendas do arquivo em vez de ler o corpo.
    public RelatorioComissoes Calcular([FromBody] ListaVendas request) => CalculadoraComissao.Calcular(request.Vendas);
}
