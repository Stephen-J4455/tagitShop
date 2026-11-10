Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Coupons
    Inherits Page

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Await LoadCouponsAsync()
    End Sub

    Private Async Function LoadCouponsAsync() As System.Threading.Tasks.Task
        Try
            LoadingIndicator.Visibility = Visibility.Visible
            EmptyCouponsState.Visibility = Visibility.Collapsed
            CouponGrid.Visibility = Visibility.Collapsed

            Await DataCache.EnsureLoadedAsync()
            Dim coupons = DataCache.Coupons
            CouponGrid.ItemsSource = coupons.OrderByDescending(Function(c) c.created_at).ToList()
            CouponGrid.Visibility = If(coupons.Count = 0, Visibility.Collapsed, Visibility.Visible)
            EmptyCouponsState.Visibility = If(coupons.Count = 0, Visibility.Visible, Visibility.Collapsed)
            TxtStatus.Text = If(AppSession.Mode = StoreMode.Live, "Live coupons from your store.", "Demo coupons stored locally.")
        Catch ex As Exception
            TxtStatus.Text = ex.Message
        Finally
            LoadingIndicator.Visibility = Visibility.Collapsed
        End Try
    End Function

    Private Async Sub BtnCreateCoupon_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New CouponDialog() With {.Owner = Window.GetWindow(Me)}
        If dialog.ShowDialog() <> True Then Return
        Try
            Await SupabaseService.CreateCouponAsync(dialog.Code, dialog.Description, dialog.DiscountType, dialog.DiscountValue, dialog.ValidUntil)
            Await LoadCouponsAsync()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Coupon could not be created", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub
End Class
