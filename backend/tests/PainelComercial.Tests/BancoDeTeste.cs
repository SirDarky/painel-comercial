using PainelComercial.Api.Estoque;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace PainelComercial.Tests;

/// <summary>Arquivo SQLite temporário, exclusivo de um teste e apagado no Dispose.</summary>
internal sealed class BancoDeTeste : IDisposable
{
    private readonly string _arquivo = Path.Combine(Path.GetTempPath(), $"painel-comercial-teste-{Guid.NewGuid():N}.db");

    // Sem pool de conexões, o arquivo fica livre para ser apagado assim que os contextos são descartados.
    public string ConnectionString => new SqliteConnectionStringBuilder { DataSource = _arquivo, Pooling = false }.ToString();

    public EstoqueDbContext CriarContexto() =>
        new(new DbContextOptionsBuilder<EstoqueDbContext>().UseSqlite(ConnectionString).Options);

    public void Dispose()
    {
        foreach (var arquivo in new[] { _arquivo, _arquivo + "-wal", _arquivo + "-shm" })
            File.Delete(arquivo);
    }
}
