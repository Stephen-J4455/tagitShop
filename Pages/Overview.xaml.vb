Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Overview
    Inherits Page

    Private orders As New List(Of LocalData.OrderItem)()
    Private products As New List(Of LocalData.ProductItem)()
    Private dashboardLoaded As Boolean

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        TxtOverviewDate.Text = DateTime.Today.ToString("dddd, MMM d")
        LoadingIndicator.Visibility = Visibility.Visible
        EmptyRecentSalesState.Visibility = Visibility.Collapsed
        Try
            products = Await SupabaseService.LoadProductsAsync()
            orders = Await SupabaseService.LoadOrdersAsync()
            dashboardLoaded = True
            RefreshDashboard()
            TxtOverviewFeedback.Text = If(AppSession.Mode = StoreMode.Live, "Connected to " & AppSession.StoreName, "Demo values are stored locally.")
        Catch ex As Exception
            TxtOverviewFeedback.Text = ex.Message
        Finally
            LoadingIndicator.Visibility = Visibility.Collapsed
        End Try
    End Sub

    Private Sub RefreshDashboard()
        Dim selectedRange = SelectedOverviewRange()
        Dim buckets = SalesAnalytics.BuildBuckets(orders, selectedRange)
        Dim periodRevenue = buckets.Sum(Function(bucket) bucket.Revenue)
        Dim periodOrderCount = buckets.Sum(Function(bucket) bucket.OrderCount)
        Dim periodLabel As String = RangeDescription(selectedRange)

        TxtPrimaryMetricLabel.Text = "Paid sales, " & periodLabel.ToLowerInvariant()
        TxtTotalSales.Text = "GHC " & periodRevenue.ToString("N2")
        TxtTotalOrders.Text = periodOrderCount.ToString("N0")
        TxtTotalProducts.Text = products.Count.ToString()
        TxtAverageOrderCaption.Text = "Across " & periodLabel.ToLowerInvariant()
        TxtAverageOrderValue.Text = "GHC " & If(periodOrderCount = 0, 0D, periodRevenue / periodOrderCount).ToString("N2")
        TxtAverageOrderCount.Text = periodOrderCount.ToString("N0") & " paid orders"
        TxtSalesChartSubtitle.Text = "Paid sales from " & buckets.First().Start.ToString("MMM d, yyyy") & " to " & DateTime.Today.ToString("MMM d, yyyy")
        Dim lowStock = products.Where(Function(product) product.quantity <= product.reorder_level).OrderBy(Function(product) product.quantity - product.reorder_level).ToList()
        TxtLowStock.Text = lowStock.Count.ToString() & " items"
        LowStockGrid.ItemsSource = lowStock.Take(5).ToList()
        TxtNoLowStock.Visibility = If(lowStock.Count = 0, Visibility.Visible, Visibility.Collapsed)
        Dim recentSales = orders.Where(Function(order) String.Equals(order.payment_status, "success", StringComparison.OrdinalIgnoreCase)).
            OrderByDescending(Function(order) ResolveSaleDate(order)).Take(6).ToList()
        RecentSalesGrid.ItemsSource = recentSales
        RecentSalesGridContainer.Visibility = If(recentSales.Count = 0, Visibility.Collapsed, Visibility.Visible)
        EmptyRecentSalesState.Visibility = If(recentSales.Count = 0, Visibility.Visible, Visibility.Collapsed)
        SalesChartRenderer.Draw(SalesChartCanvas, buckets)
    End Sub

    Private Function SelectedOverviewRange() As SalesRange
        Dim selected = TryCast(CmbOverviewRange.SelectedItem, ComboBoxItem)
        If selected Is Nothing OrElse selected.Tag Is Nothing Then Return SalesRange.Monthly

        Dim selectedRange As SalesRange
        If [Enum].TryParse(Of SalesRange)(selected.Tag.ToString(), True, selectedRange) Then Return selectedRange
        Return SalesRange.Monthly
    End Function

    Private Function RangeDescription(range As SalesRange) As String
        Select Case range
            Case SalesRange.Monthly
                Return "the last 12 months"
            Case SalesRange.CurrentMonth
                Return "this month"
            Case SalesRange.SixMonths
                Return "the last 6 months"
            Case SalesRange.CurrentYear
                Return "this year"
            Case Else
                Return "the last 7 days"
        End Select
    End Function

    Private Function ResolveSaleDate(order As LocalData.OrderItem) As DateTime
        If order.paid_at.HasValue Then Return order.paid_at.Value
        If order.occurred_at > DateTime.MinValue Then Return order.occurred_at
        Dim parsed As DateTime
        If DateTime.TryParse(order.created_at, parsed) Then Return parsed
        Return DateTime.MinValue
    End Function

    Private Sub SalesChartCanvas_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        If dashboardLoaded Then SalesChartRenderer.Draw(SalesChartCanvas, SalesAnalytics.BuildBuckets(orders, SelectedOverviewRange()))
    End Sub

    Private Sub CmbOverviewRange_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        If dashboardLoaded Then RefreshDashboard()
    End Sub

    Private Sub BtnCatalog_Click(sender As Object, e As RoutedEventArgs)
        CType(Application.Current.MainWindow, MainWindow).NavigateToPage(New Catalog())
    End Sub

    Private Sub BtnOrders_Click(sender As Object, e As RoutedEventArgs)
        CType(Application.Current.MainWindow, MainWindow).NavigateToPage(New Orders())
    End Sub
End Class