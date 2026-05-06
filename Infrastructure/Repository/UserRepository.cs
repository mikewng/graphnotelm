using graphnotelm.Core.Models;
using graphnotelm.Infrastructure.Repositories;
using graphnotelm.Infrastructure.Repository.Contracts;
using Microsoft.EntityFrameworkCore;

namespace graphnotelm.Infrastructure.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(AppDbContext context) : base(context) { }

        public async Task AddAsync(User user, CancellationToken ct = default)
        {
            await DbSet.AddAsync(user, ct);
        }

        public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        {
            return await DbSet.AnyAsync(u => u.Email == email, ct);
        }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        {
            return await DbSet.FirstOrDefaultAsync(u => u.Email == email, ct);
        }

        public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            return await DbSet.FirstOrDefaultAsync(u => u.Id == id, ct);
        }

        public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        {
            return await DbSet.FirstOrDefaultAsync(u => u.Username == username, ct);
        }

        public async Task<bool> UpdateAsync(User user, CancellationToken ct = default)
        {
            if (!await DbSet.AnyAsync(u => u.Id == user.Id, ct))
                return false;

            DbSet.Update(user);
            return true;
        }

        public async Task<bool> RemoveAsync(User user, CancellationToken ct = default)
        {
            if (!await DbSet.AnyAsync(u => u.Id == user.Id, ct))
                return false;

            DbSet.Remove(user);
            return true;
        }

        public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
        {
            return await DbSet.AnyAsync(u => u.Username == username, ct);
        }

    }
}
