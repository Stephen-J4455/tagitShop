Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input

Partial Public Class ProductPickerDialog
    Inherits Window

    Private ReadOnly productView As ICollectionView

    Public Property SelectedProduct As LocalData.ProductItem

    Public Sub New(products As IEnumerable(Of LocalData.ProductItem))
        InitializeComponent()
        ProductsList.ItemsSource = products.
            Where(Function(product) product IsNot Nothing AndAlso
                  Not String.IsNullOrWhiteSpace(product.id) AndAlso
                  String.Equals(product.status, "active", StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(product) product.title).
            ToList()
        productView = CollectionViewSource.GetDefaultView(ProductsList.ItemsSource)
        AddHandler TxtSearch.TextChanged, AddressOf TxtSearch_TextChanged
        AddHandler ProductsList.SelectionChanged, AddressOf ProductsList_SelectionChanged
        TxtResultCount.Text = ProductsList.Items.Count.ToString("N0") & " active products"
    End Sub

    Private Sub ApplySearch()
        If productView Is Nothing Then Return
        Dim query = TxtSearch.Text.Trim()
        productView.Filter = Function(item)
                                 Dim product = DirectCast(item, LocalData.ProductItem)
                                 Return String.IsNullOrWhiteSpace(query) OrElse
                                     If(product.title, String.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                     If(product.sku, String.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                             End Function
        TxtResultCount.Text = ProductsList.Items.Count.ToString("N0") & " products"
    End Sub

    Private Sub TxtSearch_TextChanged(sender As Object, e As TextChangedEventArgs)
        ApplySearch()
    End Sub

    Private Sub ProductsList_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        BtnSelect.IsEnabled = TypeOf ProductsList.SelectedItem Is LocalData.ProductItem
    End Sub

    Private Sub ProductsList_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
        If BtnSelect.IsEnabled Then SelectProduct()
    End Sub

    Private Sub BtnSelect_Click(sender As Object, e As RoutedEventArgs)
        SelectProduct()
    End Sub

    Private Sub SelectProduct()
        SelectedProduct = TryCast(ProductsList.SelectedItem, LocalData.ProductItem)
        If SelectedProduct Is Nothing Then Return
        DialogResult = True
        Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub
End Class
