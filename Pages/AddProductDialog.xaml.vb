Imports System
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class AddProductDialog
    Inherits Window

    Private ReadOnly sourceProduct As LocalData.ProductItem
    Public Property NewProduct As LocalData.ProductItem

    Public Sub New(Optional product As LocalData.ProductItem = Nothing)
        InitializeComponent()
        sourceProduct = product
        CmbCategory.SelectedIndex = 0
        If sourceProduct Is Nothing Then Return

        TxtDialogTitle.Text = "Edit product"
        BtnSave.Content = "Save changes"
        TxtProductName.Text = sourceProduct.title
        TxtSku.Text = sourceProduct.sku
        TxtBarcode.Text = sourceProduct.barcode
        TxtCostPrice.Text = sourceProduct.cost_price.ToString("0.00")
        TxtPrice.Text = sourceProduct.price.ToString("0.00")
        TxtQuantity.Text = sourceProduct.quantity.ToString()
        TxtReorderLevel.Text = sourceProduct.reorder_level.ToString()
        For Each optionItem As ComboBoxItem In CmbCategory.Items
            If optionItem.Content.ToString().Equals(sourceProduct.category, StringComparison.OrdinalIgnoreCase) Then
                CmbCategory.SelectedItem = optionItem
                Exit For
            End If
        Next
    End Sub

    Private Sub BtnAdd_Click(sender As Object, e As RoutedEventArgs)
        Dim name = TxtProductName.Text.Trim()
        If String.IsNullOrWhiteSpace(name) Then
            MessageBox.Show("Enter a product name.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim price As Decimal
        If Not Decimal.TryParse(TxtPrice.Text, price) OrElse price <= 0D Then
            MessageBox.Show("Enter a selling price greater than zero.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim costPrice As Decimal = 0D
        If Not String.IsNullOrWhiteSpace(TxtCostPrice.Text) AndAlso (Not Decimal.TryParse(TxtCostPrice.Text, costPrice) OrElse costPrice < 0D) Then
            MessageBox.Show("Enter a valid non-negative cost price.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim quantity As Integer
        If Not Integer.TryParse(TxtQuantity.Text, quantity) OrElse quantity < 0 Then
            MessageBox.Show("Enter a non-negative on-hand quantity.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim reorderLevel As Integer
        If Not Integer.TryParse(TxtReorderLevel.Text, reorderLevel) OrElse reorderLevel < 0 Then
            MessageBox.Show("Enter a non-negative reorder level.", "Product details", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim categoryItem = TryCast(CmbCategory.SelectedItem, ComboBoxItem)
        NewProduct = New LocalData.ProductItem() With {
            .id = If(sourceProduct Is Nothing, If(AppSession.Mode = StoreMode.Demo, Guid.NewGuid().ToString("N"), String.Empty), sourceProduct.id),
            .title = name,
            .price = price,
            .cost_price = costPrice,
            .sku = TxtSku.Text.Trim(),
            .barcode = TxtBarcode.Text.Trim(),
            .thumbnail = If(sourceProduct Is Nothing, "pack://application:,,,/assets/placeholder.png", sourceProduct.thumbnail),
            .seller_id = If(AppSession.Mode = StoreMode.Live, AppSession.SellerId, "local"),
            .status = If(sourceProduct Is Nothing, "active", sourceProduct.status),
            .quantity = quantity,
            .reorder_level = reorderLevel,
            .category = If(categoryItem Is Nothing, "Other", categoryItem.Content.ToString()),
            .updated_at = DateTime.Now
        }

        DialogResult = True
        Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub
End Class