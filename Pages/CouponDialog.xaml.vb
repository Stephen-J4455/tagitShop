Imports System
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class CouponDialog
    Inherits Window

    Public Property Code As String
    Public Property Description As String
    Public Property DiscountType As String
    Public Property DiscountValue As Decimal
    Public Property ValidUntil As DateTime?

    Public Sub New()
        InitializeComponent()
        CmbDiscountType.SelectedIndex = 0
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As RoutedEventArgs)
        Dim code = TxtCode.Text.Trim()
        If String.IsNullOrWhiteSpace(code) Then
            MessageBox.Show("Enter a coupon code.", "Coupon", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim description = TxtDescription.Text.Trim()
        If String.IsNullOrWhiteSpace(description) Then
            MessageBox.Show("Enter a description.", "Coupon", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim discountType = If(TryCast(CmbDiscountType.SelectedItem, ComboBoxItem)?.Content?.ToString(), "percentage")
        Dim discountValue As Decimal
        If Not Decimal.TryParse(TxtDiscountValue.Text, discountValue) OrElse discountValue <= 0D Then
            MessageBox.Show("Enter a valid discount value greater than zero.", "Coupon", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim validUntil As DateTime? = Nothing
        If Not String.IsNullOrWhiteSpace(TxtValidUntil.Text) Then
            Dim vu As DateTime
            If Not DateTime.TryParse(TxtValidUntil.Text, vu) Then
                MessageBox.Show("Enter a valid expiry date.", "Coupon", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If
            validUntil = vu
        End If

        Code = code
        Description = description
        DiscountType = discountType
        DiscountValue = discountValue
        ValidUntil = validUntil

        DialogResult = True
        Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub
End Class
