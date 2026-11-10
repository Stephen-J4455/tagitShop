Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Threading

Partial Public Class Catalog
    Inherits Page

    Private products As New List(Of LocalData.ProductItem)()
    Private productView As ICollectionView
    Private ReadOnly productsRefreshTimer As New DispatcherTimer With {.Interval = TimeSpan.FromSeconds(10)}
    Private isRefreshing As Boolean

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
        AddHandler Unloaded, AddressOf Page_Unloaded
        AddHandler productsRefreshTimer.Tick, AddressOf ProductsRefreshTimer_Tick
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        Await RefreshProductsAsync()
        If AppSession.Mode = StoreMode.Live Then productsRefreshTimer.Start()
    End Sub

    Private Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        productsRefreshTimer.Stop()
    End Sub

    Private Async Sub ProductsRefreshTimer_Tick(sender As Object, e As EventArgs)
        Await RefreshProductsAsync()
    End Sub

    Private Async Function RefreshProductsAsync() As Threading.Tasks.Task
        If isRefreshing Then Return
        isRefreshing = True
        Dim isInitialLoad = productView Is Nothing
        Dim selectedProduct = TryCast(ProductGrid.SelectedItem, LocalData.ProductItem)
        Dim selectedProductId = If(selectedProduct Is Nothing, Nothing, selectedProduct.id)
        Dim scrollOffset = PageScrollViewer.VerticalOffset
        If isInitialLoad Then
            LoadingIndicator.Visibility = Visibility.Visible
            EmptyProductsState.Visibility = Visibility.Collapsed
            ProductGridContainer.Visibility = Visibility.Collapsed
        End If

        Try
            Await DataCache.EnsureLoadedAsync()
            Dim refreshedProducts = Await DataCache.RefreshProductsAsync()
            products.Clear()
            products.AddRange(refreshedProducts)
            If productView Is Nothing Then
                ProductGrid.ItemsSource = products
                productView = CollectionViewSource.GetDefaultView(ProductGrid.ItemsSource)
            Else
                productView.Refresh()
            End If
            ApplySearch()
            TxtProductCount.Text = products.Count.ToString()
            TxtStockValue.Text = "GHC " & products.Sum(Function(product) product.price * product.quantity).ToString("N0")
            TxtLowStock.Text = products.Where(Function(product) product.quantity <= product.reorder_level).Count().ToString() & " items"
            TxtPageFeedback.Text = If(AppSession.Mode = StoreMode.Live,
                "Live product data - refreshed " & DateTime.Now.ToString("h:mm:ss tt") & " (auto-refreshes every 10 seconds)",
                "Demo data saved on this computer")
            If Not String.IsNullOrWhiteSpace(selectedProductId) Then
                ProductGrid.SelectedItem = products.FirstOrDefault(Function(product) product.id = selectedProductId)
                PageScrollViewer.ScrollToVerticalOffset(scrollOffset)
            End If
        Catch ex As Exception
            TxtPageFeedback.Text = If(products.Count = 0, "Products could not be loaded: ", "Product refresh failed: ") & ex.Message
            If products.Count = 0 Then
                TxtEmptyProductsTitle.Text = "Products could not be loaded"
                TxtEmptyProductsMessage.Text = ex.Message
                EmptyProductsState.Visibility = Visibility.Visible
            End If
        Finally
            If isInitialLoad Then LoadingIndicator.Visibility = Visibility.Collapsed
            isRefreshing = False
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
        UpdateProductListState()
    End Sub

    Private Sub UpdateProductListState()
        Dim noResults = productView Is Nothing OrElse productView.IsEmpty
        ProductGridContainer.Visibility = If(noResults, Visibility.Collapsed, Visibility.Visible)
        EmptyProductsState.Visibility = If(noResults, Visibility.Visible, Visibility.Collapsed)
        If products.Count = 0 Then
            TxtEmptyProductsTitle.Text = "No products yet"
            TxtEmptyProductsMessage.Text = "Add a product to begin managing your store inventory."
        ElseIf noResults Then
            TxtEmptyProductsTitle.Text = "No matching products"
            TxtEmptyProductsMessage.Text = "Try another product name, SKU, or barcode."
        End If
    End Sub

    Private Sub TxtSearch_TextChanged(sender As Object, e As TextChangedEventArgs)
        ApplySearch()
    End Sub

    Private Sub BtnViewProduct_Click(sender As Object, e As RoutedEventArgs)
        Dim product = TryCast(DirectCast(sender, FrameworkElement).Tag, LocalData.ProductItem)
        If product Is Nothing Then Return
        Dim dialog = New ProductDetailsDialog(product) With {.Owner = Window.GetWindow(Me)}
        dialog.ShowDialog()
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
            Await DataCache.EnsureLoadedAsync()
            Await RefreshProductsAsync()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Product could not be saved", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Async Sub BtnDeleteProduct_Click(sender As Object, e As RoutedEventArgs)
        Dim product = TryCast(DirectCast(sender, FrameworkElement).Tag, LocalData.ProductItem)
        If product Is Nothing Then Return

        Dim productName = If(String.IsNullOrWhiteSpace(product.title), "this product", """" & product.title & """")
        Dim prompt = "Delete " & productName & "?" & Environment.NewLine & Environment.NewLine &
            "Its reviews, cart and wishlist entries, and flash sales will be removed. Existing orders and reels will keep their history without a product link."
        If MessageBox.Show(prompt, "Delete product", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return

        productsRefreshTimer.Stop()
        Try
            Dim warnings = Await SupabaseService.DeleteProductAsync(product.id)
            Await RefreshProductsAsync()
            If warnings.Count = 0 Then
                TxtPageFeedback.Text = productName & " was deleted."
            Else
                TxtPageFeedback.Text = productName & " was deleted with cleanup warnings."
                MessageBox.Show(
                    "The product was deleted, but some related data could not be cleaned up:" & Environment.NewLine &
                    String.Join(Environment.NewLine, warnings),
                    "Product deleted with warnings",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning)
            End If
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Product could not be deleted", MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            If AppSession.Mode = StoreMode.Live AndAlso IsLoaded Then productsRefreshTimer.Start()
        End Try
    End Sub

    Private Async Sub BtnEditProduct_Click(sender As Object, e As RoutedEventArgs)
        Dim product = TryCast(DirectCast(sender, FrameworkElement).Tag, LocalData.ProductItem)
        If product Is Nothing Then Return
        Dim dialog = New AddProductDialog(product) With {.Owner = Window.GetWindow(Me)}
        If dialog.ShowDialog() <> True Then Return
        Try
            Await SupabaseService.SaveProductAsync(dialog.NewProduct)
            Await DataCache.EnsureLoadedAsync()
            Await RefreshProductsAsync()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Product could not be saved", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub
End Class