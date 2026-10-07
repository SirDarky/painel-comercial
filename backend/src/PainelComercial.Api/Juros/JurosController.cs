using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace PainelComercial.Api.Juros;

public sealed record CalculoJurosRequest(
    [Required(ErrorMessage = "Informe o valor.")]
    [Range(0.01, 1_000_000_000_000, ErrorMessage = "O valor deve estar entre R$ 0,01 e R$ 1.000.000.000.000,00.")]
    decimal? Valor,

    [Required(ErrorMessage = "Informe a data de vencimento (aaaa-mm-dd).")]
    DateOnly? DataVencimento);

[ApiController]
[Route("api/juros")]
public sealed class JurosController(TimeProvider relogio) : ControllerBase
{
    /// <summary>Calcula os juros de um valor na data de hoje (multa de 2,5% ao dia de atraso).</summary>
    /// <remarks>Exemplo: GET /api/juros?valor=1000&amp;dataVencimento=2026-09-26</remarks>
    [HttpGet]
    [ProducesResponseType<CalculoJuros>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public CalculoJuros Calcular([FromQuery] CalculoJurosRequest request)
    {
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().DateTime);
        return CalculadoraJuros.Calcular(request.Valor!.Value, request.DataVencimento!.Value, hoje);
    }
}
