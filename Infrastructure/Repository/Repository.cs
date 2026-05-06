using Microsoft.EntityFrameworkCore;

namespace graphnotelm.Infrastructure.Repositories
{
    public abstract class Repository<T> where T : class
    {
        protected readonly AppDbContext Context;
        protected readonly DbSet<T> DbSet;

        protected Repository(AppDbContext context)
        {
            Context = context;
            DbSet = context.Set<T>();
        }
    }
}
