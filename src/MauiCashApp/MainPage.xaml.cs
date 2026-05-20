using CommunityToolkit.Mvvm.ComponentModel;
using ShopAppVpd.Dtos;
using ShopAppVpd.Interfaces;
using ShopAppVpd.Services;
using ShopAppVpd.ViewModels;

namespace ShopAppVpd;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
    
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (ProductCollection?.ItemsLayout is GridItemsLayout grid)
        {
            double cardMinWidth = 225;
            double spacing = 15;
            double totalMargin = 30;
            
            double availableWidth = width - totalMargin;
            
            int span = Math.Max(1, (int)((availableWidth + spacing) / (cardMinWidth + spacing)));

            if (grid.Span != span)
                grid.Span = span;
        }
    }
}