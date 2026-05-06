
using BookShoppingCartMvcUI.Repositories; 

namespace BookShoppingCartMvcUI.Repositories
{
    public interface ICartRepositoryBases
    {
        Task<bool> RemoveItem(int bookId);
    }
}