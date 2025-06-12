using Data.Entities;
using Presentation.Data;

namespace Presentation.Repositories;

// both TEntity (UserEntity) and TModel (UserEntity)
public class AccountRepository(DataContext context) : BaseRepository<UserEntity, UserEntity>(context), IAccountRepository
{

   
}
