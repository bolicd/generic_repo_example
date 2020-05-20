using Infrastructure.Models;

namespace Infrastructure.Repositories
{
    public class UserRepository : GenericRepository<User>
    {
        public UserRepository(string tableName) : base(tableName)
        {
        }
    }
}
