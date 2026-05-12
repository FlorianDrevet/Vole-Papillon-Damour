using CommunityToolkit.Mvvm.ComponentModel;
using ShopAppVpd.Dtos;

namespace ShopAppVpd.ViewModels;

public partial class ShoppedProductViewModel: ObservableObject
{
    public Product Product { get; }
    
    [ObservableProperty]
    private int _quantity = 1;

    public double Total => Product.Price * Quantity;

    public ShoppedProductViewModel(Product product, int quantity = 1)
    {
        Product = product;
        Quantity = quantity;
    }
    
    public void IncrementQuantity()
    {
        Quantity++;
        OnPropertyChanged(nameof(Total));
    }
    
    public void DecrementQuantity()
    {
        if (Quantity > 0)
        {
            Quantity--;
            OnPropertyChanged(nameof(Total));
        }
    }
}