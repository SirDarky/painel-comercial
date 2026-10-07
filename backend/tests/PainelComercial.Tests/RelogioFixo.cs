namespace PainelComercial.Tests;

/// <summary>Relógio parado em um instante conhecido, para testes que dependem de "hoje".</summary>
internal sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public static readonly RelogioFixo Em06Out2026 = new(new DateTimeOffset(2026, 10, 6, 9, 30, 0, TimeSpan.FromHours(-3)));

    public override DateTimeOffset GetUtcNow() => agora.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone { get; } =
        TimeZoneInfo.CreateCustomTimeZone("Teste", agora.Offset, "Teste", "Teste");
}
