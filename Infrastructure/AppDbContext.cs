using card_library.Core.Application.Repository.Contracts;
using graphnotelm.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace graphnotelm.Infrastructure
{
    public class AppDbContext : DbContext, IUnitOfWork
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<User> Users => Set<User>();
        public DbSet<NoteGraphMetadata> NoteGraphMetadata => Set<NoteGraphMetadata>();
        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
            => base.SaveChangesAsync(ct);
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasKey(u => u.Id);

                e.Property(u => u.Id).ValueGeneratedOnAdd();
                e.Property(u => u.Username).IsRequired().HasMaxLength(50);
                e.Property(u => u.Email).IsRequired().HasMaxLength(100);
                e.Property(u => u.PasswordHash).IsRequired().HasMaxLength(255);
                e.Property(u => u.CreatedAt).IsRequired();
                e.Property(u => u.LastLoginAt);

                e.HasIndex(u => u.Username).IsUnique();
                e.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<NoteGraphMetadata>(e =>
            {
                e.ToTable("NoteGraphMetadata");
                e.HasKey(u => u.Id);

                e.Property(u => u.Id).ValueGeneratedOnAdd();
                e.Property(u => u.NoteGraphId).ValueGeneratedOnAdd();
                e.Property(u => u.UserId).IsRequired();
                e.Property(u => u.Name).IsRequired().HasMaxLength(255);
                e.Property(u => u.isPublic).IsRequired().HasDefaultValue(false);
                e.Property(u => u.isDeleted).IsRequired().HasDefaultValue(false);

            });
        }
    }
}
