using ShopAppVpd.Dtos;
using SQLite;

namespace ShopAppVpd.Databases;

public class ProductDatabase
{
    private SQLiteAsyncConnection? _database;
    
    private async Task Init()
    {
        if (_database is not null)
            return;

        _database = new SQLiteAsyncConnection(Constants.SqLite.DatabasePath, Constants.SqLite.Flags);
        var result = await _database.CreateTableAsync<Product>();
    }
    
    public async Task<List<Product>> GetProductsAsync()
    {
        await Init();
        return await _database!.Table<Product>().ToListAsync();
    }
    
    public async Task SaveProductsAsync(IEnumerable<Product> products)
    {
        await Init();
        await _database!.RunInTransactionAsync(tran =>
        {
            tran.DeleteAll<Product>();
            tran.InsertAll(products);
        });
    }
}