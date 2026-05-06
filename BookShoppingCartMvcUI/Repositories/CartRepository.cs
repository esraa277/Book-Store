using BookShoppingCartMvcUI.Data;
using BookShoppingCartMvcUI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BookShoppingCartMvcUI.Repositories
{
    public class CartRepository: ICartRepository
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IHttpContextAccessor _httpcontextAccessor;
        private readonly int bookId;

        public CartRepository(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor,
            UserManager<IdentityUser> userManager) 
        {
            _db = db;
            _userManager = userManager;
            _httpcontextAccessor = httpContextAccessor;
        }
        public async Task <int> AddItem(int bookID, int qty)
        {
            string userId = GetUserID();
            using var transaction = _db.Database.BeginTransaction();
            try
            {
               
                if (string.IsNullOrEmpty(userId))
                    throw new Exception("user is not logged-in");
                var cart = await GetCart(userId);
                if (cart is null)
                {
                    cart = new ShoppingCart
                    {
                        UserId = userId
                    };
                   _db.ShoppingCarts.Add(cart);
                }
                _db.SaveChanges();

                var cartItem = _db.CartDetails.FirstOrDefault(a => a.ShoppingCartId == cart.Id && a.BookId == bookID);
                if (cartItem != null) 
                {
                    cartItem.Quantity += qty;
                }
                else
                {
                    var book = _db.Books.Find(bookID);
                    cartItem = new CartDetail
                    {
                        BookId = bookID,
                        ShoppingCartId = cart.Id,
                        Quantity=qty,
                        UnitPrice=book.Price
                    };
                    _db.CartDetails.Add(cartItem);
                }
                _db.SaveChanges();
                transaction.Commit();
               
            }
            catch (Exception ex)
            {
               
            }
            var cartItemCount = await GetCartItemCount(userId);
            return cartItemCount;
        }


        public async Task<int> RemoveItem(int bookID)
        {
            // using var transaction = _db.Database.BeginTransaction();
            string userId = GetUserID();
            try
            {

                if (string.IsNullOrEmpty(userId))
                    throw new Exception("user is not logged-in");
                var cart = await GetCart(userId);
                if (cart is null)
                    throw new Exception("Invalid Cart");
                _db.SaveChanges();

                var cartItem = _db.CartDetails.FirstOrDefault(a => a.ShoppingCartId == cart.Id && a.BookId == bookID);

                if (cartItem is null)
                    throw new Exception(" Not items in Cart");
                else if (cartItem.Quantity == 1)
                {
                    _db.CartDetails.Remove(cartItem);
                }
                else
                {
                    cartItem.Quantity-=1;
                }
                _db.SaveChanges();
               
            }
            catch (Exception ex)
            {
                
            }
            var cartItemCount = await GetCartItemCount(userId);
            return cartItemCount;
        }

        
        public async Task<ShoppingCart> GetUserCart()
        {
            var userId = GetUserID();
            if (userId == null)
                throw  new Exception("Invalid userid");
            var shoppingCart = await _db.ShoppingCarts
               .Include(a => a.CartDetails)
               .ThenInclude(a => a.Book)
               .ThenInclude(a => a.Genre)
               .Where(a => a.UserId == userId).FirstOrDefaultAsync();
            return shoppingCart;

        }

      

        public async Task<ShoppingCart> GetCart(string userId)
        {
            var cart = await _db.ShoppingCarts.FirstOrDefaultAsync(x => x.UserId == userId);
            return cart ;
        }

        public async Task <int> GetCartItemCount(string userId="")
        {
            if (string.IsNullOrEmpty(userId))
                {
                userId = GetUserID();
                }
            var data = await (from cart in _db.ShoppingCarts
                              join cartDetail in _db.CartDetails
                              on cart.Id equals cartDetail.ShoppingCartId
                              where cart.UserId == userId
                              select new {cartDetail.Quantity}
                              ).ToListAsync();
            return data.Sum(x => x.Quantity);
        }

        public async Task<bool> DoCheckout()
        {
            using var transaction = _db.Database.BeginTransaction();
            try
            {
                var userId = GetUserID();
                if (string.IsNullOrEmpty(userId))
                    throw new Exception("User is not logged in");
                var cart = await GetCart(userId);
                if(cart is null)
                    throw new Exception("Invalid cart");
                var cartDetail = _db.CartDetails
                    .Where(a => a.ShoppingCartId == cart.Id).ToList();
                if (cartDetail.Count == 0)
                    throw new Exception("Cart is empty");
                var order = new Order
                {
                    UserId = userId,
                    CreateDate = DateTime.UtcNow,
                    OrderStatusId = 1,
                };
                _db.Orders.Add(order);
                _db.SaveChanges(); 
                foreach(var item in cartDetail)
                {
                    var orderDetail = new OrderDetail
                    {
                        BookId = item.BookId,
                        OrderId = order.Id,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                    };
                    _db.OrderDetails.Add(orderDetail);
                }
                _db.SaveChanges();

                _db.CartDetails.RemoveRange(cartDetail);
                _db.SaveChanges();
                transaction.Commit();
                return true;
            }
            catch (Exception)
            {
                return false;

            }

        }
        private string GetUserID()
        {
            var principal = _httpcontextAccessor.HttpContext.User;
            string userId = _userManager.GetUserId(principal);
            return userId;
        }

        Task<int> ICartRepository.GetCartItemCount(string userID)
        {
            return GetCartItemCount(userID);
        }
    }
}
