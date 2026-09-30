using Microsoft.EntityFrameworkCore;
using TreeEditor.Core.Constants;
using TreeEditor.Core.Entities;

namespace TreeEditor.Core.Data;

public class TreeDbContext(DbContextOptions<TreeDbContext> options) : DbContext(options)
{
    public DbSet<TreeNode> Nodes => Set<TreeNode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TreeNode>(e =>
        {
            e.ToTable("Nodes");
            e.HasKey(n => n.Id);
            e.Property(n => n.Value).HasMaxLength(TreeConstants.MaxValueLength).IsRequired();
            e.Property(n => n.Path).HasMaxLength(2000).IsRequired();
            e.Ignore(n => n.ChildPath);

            e.HasOne(n => n.Parent)
                .WithMany()
                .HasForeignKey(n => n.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(n => n.ParentId);
            e.HasIndex(n => n.Path);
        });
    }
}
