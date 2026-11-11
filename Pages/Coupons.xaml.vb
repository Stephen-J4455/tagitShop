Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Threading

Partial Public Class Coupons
    Inherits Page

    Private ReadOnly couponsRefreshTimer As New DispatcherTimer With {.Interval = TimeSpan.FromSeconds(10)}
    Private isRefreshing As Boolean

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
        AddHandler Unloaded, AddressOf Page_Unloaded
        AddHandler couponsRefreshTimer.Tick, AddressOf CouponsRefreshTimer_Tick
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Await RefreshCouponsAsync()
        If AppSession.Mode = StoreMode.Live Then couponsRefreshTimer.Start()
    End Sub

    Private Sub Page_Unloaded(sender As Object, e As RoutedEventArgs)
        couponsRefreshTimer.Stop()
    End Sub

    Private Async Sub CouponsRefreshTimer_Tick(sender As Object, e As EventArgs)
        Await RefreshCouponsAsync()
    End Sub

    Private Async Function RefreshCouponsAsync() As System.Threading.Tasks.Task
        If isRefreshing Then Return
        isRefreshing = True
        Try
            LoadingIndicator.Visibility = Visibility.Visible
            EmptyCouponsState.Visibility = Visibility.Collapsed
            CouponGrid.Visibility = Visibility.Collapsed

            Await DataCache.EnsureLoadedAsync()
            Dim coupons = Await DataCache.RefreshCouponsAsync()
            CouponGrid.ItemsSource = coupons.OrderByDescending(Function(c) c.created_at).ToList()
            CouponGrid.Visibility = If(coupons.Count = 0, Visibility.Collapsed, Visibility.Visible)
            EmptyCouponsState.Visibility = If(coupons.Count = 0, Visibility.Visible, Visibility.Collapsed)
            TxtStatus.Text = If(AppSession.Mode = StoreMode.Live,
                "Live coupons - refreshed " & DateTime.Now.ToString("h:mm:ss tt") & " (auto-refreshes every 10 seconds)",
                "Demo coupons stored locally.")
        Catch ex As Exception
            TxtStatus.Text = ex.Message
        Finally
            LoadingIndicator.Visibility = Visibility.Collapsed
            isRefreshing = False
        End Try
    End Function

    Private Async Sub BtnCreateCoupon_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New CouponDialog() With {.Owner = Window.GetWindow(Me)}
        If dialog.ShowDialog() <> True Then Return
        Dim code = If(dialog.Code, String.Empty).Trim().ToUpper()
        Try
            Await SupabaseService.CreateCouponAsync(code, dialog.Description, dialog.DiscountType, dialog.DiscountValue, dialog.ValidUntil)
            Await RefreshCouponsAsync()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Coupon could not be created", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Async Sub BtnDeleteCoupon_Click(sender As Object, e As RoutedEventArgs)
        Dim coupon = TryCast(DirectCast(sender, FrameworkElement).Tag, SupabaseService.CouponItem)
        If coupon Is Nothing Then Return

        Dim code = If(String.IsNullOrWhiteSpace(coupon.code), "this coupon", """" & coupon.code & """")
        If MessageBox.Show("Delete " & code & "?" & Environment.NewLine & Environment.NewLine &
                           "This cannot be undone.", "Delete coupon", MessageBoxButton.YesNo, MessageBoxImage.Warning) <> MessageBoxResult.Yes Then Return

        couponsRefreshTimer.Stop()
        Try
            Await SupabaseService.DeleteCouponAsync(coupon.id)
            Await RefreshCouponsAsync()
            TxtStatus.Text = code & " was deleted."
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Coupon could not be deleted", MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            If AppSession.Mode = StoreMode.Live AndAlso IsLoaded Then couponsRefreshTimer.Start()
        End Try
    End Sub
End Class
