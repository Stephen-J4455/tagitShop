Imports System
Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Coupons
    Inherits Page

    Public Class CouponItem
        Public Property code As String
        Public Property discount As Integer
        Public Property description As String
        Public Property status As String
        Public Property uses As Integer
    End Class

    Public Sub New()
        InitializeComponent()
        LoadLocalCoupons()
    End Sub

    Private Sub LoadLocalCoupons()
        Try
            Dim coupons = New List(Of CouponItem)() From {
                New CouponItem() With {.code = "WELCOME20", .discount = 20, .description = "20% off first order", .status = "Active", .uses = 45},
                New CouponItem() With {.code = "SAVE10", .discount = 10, .description = "10% off orders over GHC 5000", .status = "Active", .uses = 32},
                New CouponItem() With {.code = "FREESHIP", .discount = 100, .description = "Free delivery on all orders", .status = "Active", .uses = 28},
                New CouponItem() With {.code = "BLACKFRIDAY", .discount = 30, .description = "30% off everything", .status = "Expired", .uses = 156}
            }

            CouponGrid.ItemsSource = coupons
            TxtStatus.Text = $"Loaded {coupons.Count} coupons (local)"
        Catch ex As Exception
            TxtStatus.Text = "Coupons load error: " & ex.Message
        End Try
    End Sub

    Private Sub BtnCreateCoupon_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show("Create a new discount coupon with code, percentage, and expiry.", "Create Coupon", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub
End Class
