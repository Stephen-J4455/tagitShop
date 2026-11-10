Imports System
Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data

Partial Public Class ProductDetailsDialog
    Inherits Window

    Public Sub New(product As LocalData.ProductItem)
        InitializeComponent()
        If product Is Nothing Then Throw New ArgumentNullException(NameOf(product))

        TxtName.Text = If(String.IsNullOrWhiteSpace(product.title), "Untitled product", product.title)
        TxtStatus.Text = If(String.IsNullOrWhiteSpace(product.status), "Status unavailable", product.status)
        TxtPrice.Text = "GHC " & product.price.ToString("N2")
        TxtQuantity.Text = product.quantity.ToString("N0")
        TxtCategory.Text = ValueOrFallback(product.category)
        TxtSku.Text = ValueOrFallback(product.sku)
        TxtCostPrice.Text = "GHC " & product.cost_price.ToString("N2")
        TxtReorderLevel.Text = product.reorder_level.ToString("N0")
        TxtDescription.Text = ValueOrFallback(product.description)
        ImgProduct.SetBinding(Image.SourceProperty, New Binding("image") With {.Source = product})

        Dim options As New List(Of String)()
        If product.sizes IsNot Nothing AndAlso product.sizes.Count > 0 Then options.Add("Sizes: " & String.Join(", ", product.sizes))
        If product.colors IsNot Nothing AndAlso product.colors.Count > 0 Then options.Add("Colors: " & String.Join(", ", product.colors))
        If product.tags IsNot Nothing AndAlso product.tags.Count > 0 Then options.Add("Tags: " & String.Join(", ", product.tags))
        TxtOptions.Text = If(options.Count = 0, "No options added.", String.Join(Environment.NewLine, options))
    End Sub

    Private Function ValueOrFallback(value As String) As String
        Return If(String.IsNullOrWhiteSpace(value), "Not provided", value)
    End Function

    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        Close()
    End Sub
End Class
