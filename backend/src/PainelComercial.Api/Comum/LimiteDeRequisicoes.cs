using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace PainelComercial.Api.Comum;

/// <summary>Limites por IP, por minuto. Configuráveis na seção "LimitesDeRequisicao" do appsettings.</summary>
public sealed class LimitesDeRequisicao
{
    public int RequisicoesPorMinuto { get; set; } = 120;
    public int EscritasPorMinuto { get; set; } = 30;
}

/// <summary>
/// Rate limit por IP: um limite geral para todas as rotas e outro, menor, para as rotas que
/// gravam ou processam dados enviados pelo cliente (política <see cref="Escrita"/>).
/// </summary>
public static class LimiteDeRequisicoes
{
    public const string Escrita = "escrita";

    public static IServiceCollection AddLimiteDeRequisicoes(this IServiceCollection servicos, IConfiguration configuracao)
    {
        servicos.Configure<LimitesDeRequisicao>(configuracao.GetSection("LimitesDeRequisicao"));

        return servicos.AddRateLimiter(opcoes =>
        {
            opcoes.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                contexto => JanelaPorIp(contexto, "geral", limites => limites.RequisicoesPorMinuto));
            opcoes.AddPolicy(Escrita, contexto => JanelaPorIp(contexto, Escrita, limites => limites.EscritasPorMinuto));
            opcoes.OnRejected = ResponderLimiteAtingidoAsync;
        });
    }

    private static RateLimitPartition<string> JanelaPorIp(
        HttpContext contexto, string grupo, Func<LimitesDeRequisicao, int> limite)
    {
        var limites = contexto.RequestServices.GetRequiredService<IOptions<LimitesDeRequisicao>>().Value;
        var ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

        return RateLimitPartition.GetFixedWindowLimiter($"{grupo}:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limite(limites),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    }

    private static async ValueTask ResponderLimiteAtingidoAsync(OnRejectedContext contexto, CancellationToken cancellationToken)
    {
        var http = contexto.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString();

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Muitas requisições",
                Detail = "Limite de requisições atingido. Aguarde um instante e tente novamente."
            }
        });
    }
}
