Imports System
Imports System.Globalization
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class FlashSaleDialog
    Inherits Window

    Public Property ProductId As String
    Public Property FlashPrice As Decimal
    Public Property StartTime As DateTime
    Public Property EndTime As DateTime
    Public Property MaxQuantity As Integer?
    Private selectedProduct As LocalData.ProductItem

    Public Sub New()
        InitializeComponent()
        Me.StartTime = DateTime.Now
        Me.EndTime = Me.StartTime.AddDays(1)
        TxtStartTime.Text = Me.StartTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
        TxtEndTime.Text = Me.EndTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
        AddHandler Loaded, AddressOf Dialog_Loaded
    End Sub

    Private Async Sub Dialog_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Dialog_Loaded
        Try
            Await DataCache.EnsureLoadedAsync()
            Dim availableProducts = DataCache.Products.
                Where(Function(product) product IsNot Nothing AndAlso
                      Not String.IsNullOrWhiteSpace(product.id) AndAlso
                      String.Equals(product.status, "active", StringComparison.OrdinalIgnoreCase)).
                OrderBy(Function(product) product.title).
                ToList()

            If availableProducts.Count = 0 Then
                TxtProductAvailability.Text = "No active products are available. Add or activate a product before creating a flash sale."
                BtnSave.IsEnabled = False
            Else
                TxtProductAvailability.Text = availableProducts.Count.ToString("N0") & If(availableProducts.Count = 1, " active product available.", " active products available.")
                BtnChooseProduct.IsEnabled = True
                BtnChooseProduct.Focus()
            End If
        Catch ex As Exception
            TxtProductAvailability.Text = "Products could not be loaded: " & ex.Message
            BtnChooseProduct.IsEnabled = False
            BtnSave.IsEnabled = False
        End Try
    End Sub

    Private Sub BtnChooseProduct_Click(sender As Object, e As RoutedEventArgs)
        Dim picker = New ProductPickerDialog(DataCache.Products) With {.Owner = Me}
        If picker.ShowDialog() <> True OrElse picker.SelectedProduct Is Nothing Then Return

        selectedProduct = picker.SelectedProduct
        TxtSelectedProductName.Text = selectedProduct.title
        TxtProductStatus.Text = If(String.IsNullOrWhiteSpace(selectedProduct.sku), "Active product", "SKU " & selectedProduct.sku)
        TxtOriginalPrice.Text = selectedProduct.price.ToString("0.00", CultureInfo.CurrentCulture)
        BtnChooseProduct.Content = "Change product"
        SelectedProductPanel.Visibility = Visibility.Visible
        BtnSave.IsEnabled = True
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As RoutedEventArgs)
        If selectedProduct Is Nothing OrElse String.IsNullOrWhiteSpace(selectedProduct.id) Then
            MessageBox.Show("Choose a product from the product picker.", "Flash Sale", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim parsedFlashPrice As Decimal
        If Not Decimal.TryParse(TxtFlashPrice.Text, parsedFlashPrice) OrElse parsedFlashPrice <= 0D Then
            MessageBox.Show("Enter a valid flash price greater than zero.", "Flash Sale", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim originalPrice = selectedProduct.price
        If originalPrice <= 0D Then
            MessageBox.Show("The selected product needs a price greater than zero.", "Flash Sale", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        If parsedFlashPrice >= originalPrice Then
            MessageBox.Show("Flash price must be lower than original price.", "Flash Sale", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim parsedStartTime As DateTime
        If Not DateTime.TryParse(TxtStartTime.Text, parsedStartTime) Then
            MessageBox.Show("Enter a valid start time.", "Flash Sale", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim parsedEndTime As DateTime
        If Not DateTime.TryParse(TxtEndTime.Text, parsedEndTime) Then
            MessageBox.Show("Enter a valid end time.", "Flash Sale", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        If parsedEndTime <= parsedStartTime Then
            MessageBox.Show("End time must be after start time.", "Flash Sale", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim parsedMaxQuantity As Integer? = Nothing
        If Not String.IsNullOrWhiteSpace(TxtMaxQuantity.Text) Then
            Dim mq As Integer
            If Not Integer.TryParse(TxtMaxQuantity.Text, mq) OrElse mq <= 0 Then
                MessageBox.Show("Enter a valid positive max quantity.", "Flash Sale", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If
            parsedMaxQuantity = mq
        End If

        ProductId = selectedProduct.id
        FlashPrice = parsedFlashPrice
        Me.StartTime = parsedStartTime
        Me.EndTime = parsedEndTime
        MaxQuantity = parsedMaxQuantity

        DialogResult = True
        Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub
End Class
