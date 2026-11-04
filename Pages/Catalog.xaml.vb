Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input

Partial Public Class Catalog
    Inherits Page

    Private products As New List(Of LocalData.ProductItem)()
    Private productView As ICollectionView

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Await RefreshProductsAsync()
    End Sub

    Private Async Function RefreshProductsAsync() As Threading.Tasks.Task
        Try
            products = Await SupabaseService.LoadProductsAsync()
            ProductGrid.ItemsSource = products
            productView = CollectionViewSource.GetDefaultView(ProductGrid.ItemsSource)
            ApplySearch()
            TxtProductCount.Text = products.Count.ToString()
            TxtStockValue.Text = "GHC " & products.Sum(Function(product) product.price * product.quantity).ToString("N0")
            TxtLowStock.Text = products.Where(Function(product) product.quantity <= product.reorder_level).Count().ToString() & " items"
            TxtPageFeedback.Text = If(AppSession.Mode = StoreMode.Live, "Live product data", "Demo data saved on this computer")
        Catch ex As Exception
            products = New List(Of LocalData.ProductItem)()
            ProductGrid.ItemsSource = products
            TxtPageFeedback.Text = ex.Message
        End Try
    End Function

    Private Sub ApplySearch()
        If productView Is Nothing Then Return
        Dim query = TxtSearch.Text.Trim()
        productView.Filter = Function(item)
                                 Dim product = DirectCast(item, LocalData.ProductItem)
                                 Return String.IsNullOrWhiteSpace(query) OrElse
                                     product.title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                     If(product.sku, String.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                     If(product.barcode, String.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                             End Function
    End Sub

    Private Sub TxtSearch_TextChanged(sender As Object, e As TextChangedEventArgs)
        ApplySearch()
    End Sub

    Private Sub PageScrollViewer_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs)
        PageScrollViewer.ScrollToVerticalOffset(PageScrollViewer.VerticalOffset - e.Delta)
        e.Handled = True
    End Sub

    Private Async Sub BtnAddProduct_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New AddProductDialog() With {.Owner = Window.GetWindow(Me)}
        If dialog.ShowDialog() <> True Then Return
        Try
            Await SupabaseService.SaveProductAsync(dialog.NewProduct)
            Await RefreshProductsAsync()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Product could not be saved", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Async Sub BtnEditProduct_Click(sender As Object, e As RoutedEventArgs)
        Dim product = TryCast(DirectCast(sender, FrameworkElement).Tag, LocalData.ProductItem)
        If product Is Nothing Then Return
        Dim dialog = New AddProductDialog(product) With {.Owner = Window.GetWindow(Me)}
        If dialog.ShowDialog() <> True Then Return
        Try
            Await SupabaseService.SaveProductAsync(dialog.NewProduct)
            Await RefreshProductsAsync()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Product could not be saved", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub
End Class