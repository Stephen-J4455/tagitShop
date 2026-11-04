Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Overview
    Inherits Page

    Private orders As New List(Of LocalData.OrderItem)()
    Private products As New List(Of LocalData.ProductItem)()

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        TxtOverviewDate.Text = DateTime.Today.ToString("dddd, MMM d")
        Try
            products = Await SupabaseService.LoadProductsAsync()
            orders = Await SupabaseService.LoadOrdersAsync()
            RefreshDashboard()
            TxtOverviewFeedback.Text = If(AppSession.Mode = StoreMode.Live, "Connected to " & AppSession.StoreName, "Demo values are stored locally.")
        Catch ex As Exception
            TxtOverviewFeedback.Text = ex.Message
        End Try
    End Sub

    Private Sub RefreshDashboard()
        Dim dailyBuckets = SalesAnalytics.BuildBuckets(orders, SalesRange.Daily)
        Dim todayBucket = dailyBuckets.Last()
        TxtTotalSales.Text = "GHC " & todayBucket.Revenue.ToString("N2")
        TxtTotalOrders.Text = todayBucket.OrderCount.ToString()
        TxtTotalProducts.Text = products.Count.ToString()
        Dim lowStock = products.Where(Function(product) product.quantity <= product.reorder_level).OrderBy(Function(product) product.quantity - product.reorder_level).ToList()
        TxtLowStock.Text = lowStock.Count.ToString() & " items"
        LowStockGrid.ItemsSource = lowStock.Take(5).ToList()
        TxtNoLowStock.Visibility = If(lowStock.Count = 0, Visibility.Visible, Visibility.Collapsed)
        RecentSalesGrid.ItemsSource = orders.Where(Function(order) String.Equals(order.payment_status, "success", StringComparison.OrdinalIgnoreCase)).
            OrderByDescending(Function(order) ResolveSaleDate(order)).Take(6).ToList()
        SalesChartRenderer.Draw(SalesChartCanvas, dailyBuckets)
    End Sub

    Private Function ResolveSaleDate(order As LocalData.OrderItem) As DateTime
        If order.paid_at.HasValue Then Return order.paid_at.Value
        If order.occurred_at > DateTime.MinValue Then Return order.occurred_at
        Dim parsed As DateTime
        If DateTime.TryParse(order.created_at, parsed) Then Return parsed
        Return DateTime.MinValue
    End Function

    Private Sub SalesChartCanvas_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        If orders.Count > 0 Then SalesChartRenderer.Draw(SalesChartCanvas, SalesAnalytics.BuildBuckets(orders, SalesRange.Daily))
    End Sub

    Private Sub BtnStocktake_Click(sender As Object, e As RoutedEventArgs)
        CType(Application.Current.MainWindow, MainWindow).NavigateToPage(New Stocktake())
    End Sub

    Private Sub BtnCatalog_Click(sender As Object, e As RoutedEventArgs)
        CType(Application.Current.MainWindow, MainWindow).NavigateToPage(New Catalog())
    End Sub

    Private Sub BtnOrders_Click(sender As Object, e As RoutedEventArgs)
        CType(Application.Current.MainWindow, MainWindow).NavigateToPage(New Orders())
    End Sub
End Class