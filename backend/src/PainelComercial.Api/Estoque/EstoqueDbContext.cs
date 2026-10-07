using Microsoft.EntityFrameworkCore;

namespace PainelComercial.Api.Estoque;

public sealed class EstoqueDbContext(DbContextOptions<EstoqueDbContext> options) : DbContext(options)
{
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Movimentacao> Movimentacoes => Set<Movimentacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Produto>(produto =>
        {
            produto.HasKey(p => p.CodigoProduto);
            produto.Property(p => p.CodigoProduto).ValueGeneratedNever(); // código vem do cadastro (estoque.json)
            produto.Property(p => p.DescricaoProduto).HasMaxLength(200);
            // Última barreira contra saldo negativo, além da validação no UPDATE do EstoqueService.
            produto.ToTable(tabela => tabela.HasCheckConstraint("CK_Produtos_EstoqueNaoNegativo", "\"Estoque\" >= 0"));
        });

        modelBuilder.Entity<Movimentacao>(movimentacao =>
        {
            movimentacao.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(10);
            movimentacao.Property(m => m.Descricao).HasMaxLength(200);
            movimentacao.Property(m => m.ChaveIdempotencia).HasMaxLength(100);
            movimentacao.HasIndex(m => m.ChaveIdempotencia).IsUnique();
            // O histórico é um registro de auditoria: um produto com movimentações não pode ser apagado.
            movimentacao.HasOne(m => m.Produto).WithMany().HasForeignKey(m => m.CodigoProduto)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
