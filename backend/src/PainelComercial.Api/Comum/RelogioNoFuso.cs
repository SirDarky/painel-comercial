namespace PainelComercial.Api.Comum;

/// <summary>
/// Relógio do sistema com "hoje" calculado no fuso configurado (America/Sao_Paulo), e não no fuso
/// da máquina onde a API roda. Evita que os juros mudem de dia antes da meia-noite de Brasília.
/// </summary>
public sealed class RelogioNoFuso(TimeZoneInfo fuso) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => fuso;
}
