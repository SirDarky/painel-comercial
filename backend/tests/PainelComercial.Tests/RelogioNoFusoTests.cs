using PainelComercial.Api.Comum;

namespace PainelComercial.Tests;

public class RelogioNoFusoTests
{
    [Fact]
    public void Usa_o_horario_de_brasilia_independente_do_fuso_da_maquina()
    {
        var relogio = new RelogioNoFuso(TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));

        var agora = relogio.GetLocalNow();

        Assert.Equal(TimeSpan.FromHours(-3), agora.Offset); // Brasília não tem horário de verão desde 2019
        Assert.InRange(agora.UtcDateTime - DateTime.UtcNow, TimeSpan.FromSeconds(-5), TimeSpan.FromSeconds(5));
    }
}
