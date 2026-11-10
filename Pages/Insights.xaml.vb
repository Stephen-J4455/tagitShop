Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls

Public Class InsightsValueRow
    Public Property Name As String
    Public Property ValueLabel As String
End Class

Public Class InsightsOrderFlowRow
    Public Property Label As String
    Public Property Count As Integer
End Class

Public Class InsightsSaleRow
    Public Property OrderNumber As String
    Public Property CustomerName As String
    Public Property DateLabel As String
    Public Property Total As Decimal
End Class

Partial Public Class Insights
    Inherits Page

    Private orders As New List(Of LocalData.OrderItem)()
    Private products As New List(Of LocalData.ProductItem)()
    Private reviews As New List(Of LocalData.ReviewItem)()
    Private analyticsLoaded As Boolean

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        LoadingIndicator.Visibility = Visibility.Visible
        EmptyInsightsState.Visibility = Visibility.Collapsed
        Try
            Await DataCache.EnsureLoadedAsync()
            orders = DataCache.Orders.ToList()
            products = DataCache.Products.ToList()
            reviews = DataCache.Reviews.ToList()
            analyticsLoaded = True
            RefreshAnalytics()
            TxtInsightsFeedback.Text = If(AppSession.Mode = StoreMode.Live,
                "Paid revenue is shown before fees; net revenue subtracts order service fees. Gross profit needs item cost data, which the live order feed does not include. Product sales are lifetime totals.",
                "Demo orders include sample cost data. Product units sold use recorded lifetime product totals.")
        Catch ex As Exception
            TxtInsightsFeedback.Text = ex.Message
        Finally
            LoadingIndicator.Visibility = Visibility.Collapsed
        End Try
    End Sub

    Private Function SelectedRange() As SalesRange
        Dim selected = TryCast(CmbRange.SelectedItem, ComboBoxItem)
        If selected Is Nothing OrElse selected.Tag Is Nothing Then Return SalesRange.Daily
        Select Case selected.Tag.ToString()
            Case "Monthly"
                Return SalesRange.Monthly
            Case "Yearly"
                Return SalesRange.Yearly
            Case Else
                Return SalesRange.Daily
        End Select
    End Function

    Private Sub RefreshAnalytics()
        If Not analyticsLoaded Then Return

        Dim range = SelectedRange()
        Dim today = DateTime.Today
        Dim buckets = SalesAnalytics.BuildBuckets(orders, range, today)
        Dim revenue = buckets.Sum(Function(bucket) bucket.Revenue)
        Dim paidOrders = buckets.Sum(Function(bucket) bucket.OrderCount)
        Dim currentOrders = OrdersInRange(orders, buckets, range)
        Dim paidOrderRows = currentOrders.
            Where(Function(order) IsPaid(order)).
            OrderByDescending(Function(order) ResolveOrderDate(order)).
            ToList()
        Dim pendingOrders = currentOrders.Where(Function(order) IsAwaitingPayment(order)).ToList()
        Dim otherPaymentCount = currentOrders.Where(Function(order) Not IsPaid(order) AndAlso Not IsAwaitingPayment(order)).Count()
        Dim grossProfit = SumGrossProfit(buckets)
        Dim previousReferenceDate = SelectCaseRangeOffset(today, range)
        Dim previousRevenue = SalesAnalytics.BuildBuckets(orders, range, previousReferenceDate).Sum(Function(bucket) bucket.Revenue)
        Dim paidOrderCount = paidOrderRows.Count
        Dim collectionRate = If(currentOrders.Count = 0, 0D, paidOrderCount * 100D / currentOrders.Count)
        Dim lowStock = products.
            Where(Function(product) product.track_inventory AndAlso
                  String.Equals(product.status, "active", StringComparison.OrdinalIgnoreCase) AndAlso
                  product.quantity <= product.reorder_level).
            OrderBy(Function(product) product.quantity - product.reorder_level).
            ThenBy(Function(product) product.quantity).
            ToList()

        TxtRangeDescription.Text = RangeLabel(range)
        TxtTotalRevenue.Text = "GHC " & revenue.ToString("N2")
        TxtNetRevenue.Text = "GHC " & currentOrders.
            Where(Function(order) IsPaid(order)).
            Sum(Function(order) order.total - order.service_fee).ToString("N2")
        TxtGrossProfit.Text = If(grossProfit.HasValue, "GHC " & grossProfit.Value.ToString("N2"), "Unavailable")
        TxtAvgOrder.Text = "GHC " & If(paidOrderCount = 0, 0D, revenue / paidOrderCount).ToString("N2")
        TxtChartTitle.Text = "Paid revenue and gross profit / " & RangeLabel(range).ToLowerInvariant()
        TxtRevenueChange.Text = RevenueChangeLabel(revenue, previousRevenue)
        TxtCollectionRate.Text = collectionRate.ToString("0") & "%"
        CollectionRateBar.Value = Math.Min(100D, Math.Max(0D, collectionRate))
        TxtPaidOrderCount.Text = paidOrderCount.ToString("N0") & If(paidOrderCount = 1, " paid order", " paid orders")
        TxtPendingOrderCount.Text = pendingOrders.Count.ToString("N0") & " awaiting payment"
        TxtPendingValue.Text = "GHC " & pendingOrders.Sum(Function(order) order.total).ToString("N2")
        TxtOtherPaymentCount.Text = otherPaymentCount.ToString("N0") & If(otherPaymentCount = 1, " failed or refunded", " failed or refunded")
        TxtOrderFlowRange.Text = RangeShortLabel(range)

        Dim topProducts = products.
            Where(Function(product) product.sold_count > 0).
            OrderByDescending(Function(product) product.sold_count).
            ThenBy(Function(product) product.title).
            Take(3).
            Select(Function(product) New InsightsValueRow() With {
                .Name = If(String.IsNullOrWhiteSpace(product.title), "Product", product.title),
                .ValueLabel = product.sold_count.ToString("N0") & " sold"
            }).ToList()
        TopProductsList.ItemsSource = topProducts
        TxtNoTopProducts.Visibility = If(topProducts.Count = 0, Visibility.Visible, Visibility.Collapsed)

        TxtLifetimeUnitsSold.Text = products.Sum(Function(product) product.sold_count).ToString("N0")
        TxtActiveProductCount.Text = products.Where(Function(product) String.Equals(product.status, "active", StringComparison.OrdinalIgnoreCase)).Count().ToString("N0")
        TxtAverageRating.Text = If(reviews.Count = 0, "—", reviews.Average(Function(review) review.rating).ToString("0.0") & " / 5")
        TxtReviewCount.Text = reviews.Count.ToString("N0") & If(reviews.Count = 1, " approved review", " approved reviews")

        Dim statusRows = BuildOrderFlowRows(currentOrders)
        OrderFlowList.ItemsSource = statusRows
        TxtLowStockCount.Text = lowStock.Count.ToString("N0") & If(lowStock.Count = 1, " product", " products")
        Dim lowStockRows = lowStock.Take(3).
            Select(Function(product) New InsightsValueRow() With {
                .Name = If(String.IsNullOrWhiteSpace(product.title), "Product", product.title),
                .ValueLabel = product.quantity.ToString("N0") & " left"
            }).ToList()
        LowStockList.ItemsSource = lowStockRows
        TxtNoLowStock.Visibility = If(lowStockRows.Count = 0, Visibility.Visible, Visibility.Collapsed)

        Dim recentSales = paidOrderRows.Take(5).
            Select(Function(order) New InsightsSaleRow() With {
                .OrderNumber = If(String.IsNullOrWhiteSpace(order.order_number), "Order", order.order_number),
                .CustomerName = If(String.IsNullOrWhiteSpace(order.customer_name), "Customer", order.customer_name.Trim()),
                .DateLabel = ResolveOrderDate(order).ToString("MMM d, h:mm tt"),
                .Total = order.total
            }).ToList()
        RecentSalesGrid.ItemsSource = recentSales
        RecentSalesGrid.Visibility = If(recentSales.Count = 0, Visibility.Collapsed, Visibility.Visible)
        TxtNoRecentSales.Visibility = If(recentSales.Count = 0, Visibility.Visible, Visibility.Collapsed)
        TxtRecentSalesCount.Text = paidOrderCount.ToString("N0") & If(paidOrderCount = 1, " order", " orders")

        EmptyInsightsState.Visibility = If(paidOrders = 0, Visibility.Visible, Visibility.Collapsed)
        SalesChartRenderer.Draw(RevenueChartCanvas, buckets)
    End Sub

    Private Function SelectCaseRangeOffset(today As DateTime, range As SalesRange) As DateTime
        Select Case range
            Case SalesRange.Monthly
                Return today.AddMonths(-12)
            Case SalesRange.Yearly
                Return today.AddYears(-5)
            Case Else
                Return today.AddDays(-7)
        End Select
    End Function

    Private Function RangeLabel(range As SalesRange) As String
        Select Case range
            Case SalesRange.Monthly
                Return "Last 12 months"
            Case SalesRange.Yearly
                Return "Last 5 years"
            Case Else
                Return "Last 7 days"
        End Select
    End Function

    Private Function RangeShortLabel(range As SalesRange) As String
        Select Case range
            Case SalesRange.Monthly
                Return "12 months"
            Case SalesRange.Yearly
                Return "5 years"
            Case Else
                Return "7 days"
        End Select
    End Function

    Private Function RevenueChangeLabel(currentRevenue As Decimal, previousRevenue As Decimal) As String
        If previousRevenue = 0D Then Return If(currentRevenue = 0D, "No change", "New in this period")
        Dim change = (currentRevenue - previousRevenue) * 100D / previousRevenue
        Dim direction = If(change > 0D, "+", String.Empty)
        Return direction & change.ToString("0.0") & "% vs prior period"
    End Function

    Private Function OrdersInRange(source As IEnumerable(Of LocalData.OrderItem),
                                   buckets As IReadOnlyList(Of SalesBucket),
                                   range As SalesRange) As List(Of LocalData.OrderItem)
        If buckets Is Nothing OrElse buckets.Count = 0 Then Return New List(Of LocalData.OrderItem)()
        Dim startDate = buckets(0).Start.Date
        Dim endDate As DateTime
        Select Case range
            Case SalesRange.Monthly
                endDate = buckets(buckets.Count - 1).Start.AddMonths(1)
            Case SalesRange.Yearly
                endDate = buckets(buckets.Count - 1).Start.AddYears(1)
            Case Else
                endDate = buckets(buckets.Count - 1).Start.AddDays(1)
        End Select
        Return source.
            Where(Function(order)
                      Dim orderDate = ResolveOrderDate(order)
                      Return orderDate >= startDate AndAlso orderDate < endDate
                  End Function).
            ToList()
    End Function

    Private Function BuildOrderFlowRows(currentOrders As IEnumerable(Of LocalData.OrderItem)) As List(Of InsightsOrderFlowRow)
        Return New List(Of InsightsOrderFlowRow) From {
            New InsightsOrderFlowRow() With {
                .Label = "Awaiting payment",
                .Count = currentOrders.Count(Function(order) IsAwaitingPayment(order))
            },
            New InsightsOrderFlowRow() With {
                .Label = "Preparing",
                .Count = currentOrders.Count(Function(order) IsStatus(order, "processing", "confirmed", "packed"))
            },
            New InsightsOrderFlowRow() With {
                .Label = "On the way",
                .Count = currentOrders.Count(Function(order) IsStatus(order, "shipped"))
            },
            New InsightsOrderFlowRow() With {
                .Label = "Delivered",
                .Count = currentOrders.Count(Function(order) IsStatus(order, "delivered"))
            },
            New InsightsOrderFlowRow() With {
                .Label = "Canceled or refunded",
                .Count = currentOrders.Count(Function(order) IsStatus(order, "canceled", "refunded") OrElse IsPaymentStatus(order, "refunded"))
            }
        }
    End Function

    Private Function SumGrossProfit(buckets As IEnumerable(Of SalesBucket)) As Decimal?
        Dim grossProfit As Decimal? = 0D
        For Each bucket In buckets
            If bucket.OrderCount > 0 AndAlso Not bucket.GrossProfit.HasValue Then Return Nothing
            If grossProfit.HasValue Then grossProfit += bucket.GrossProfit.GetValueOrDefault()
        Next
        Return grossProfit
    End Function

    Private Function IsPaid(order As LocalData.OrderItem) As Boolean
        Return IsPaymentStatus(order, "success")
    End Function

    Private Function IsAwaitingPayment(order As LocalData.OrderItem) As Boolean
        If IsPaid(order) Then Return False
        If IsPaymentStatus(order, "pending") Then Return True
        Return String.IsNullOrWhiteSpace(order.payment_status) AndAlso IsStatus(order, "pending_payment")
    End Function

    Private Function IsPaymentStatus(order As LocalData.OrderItem, expected As String) As Boolean
        Return String.Equals(If(order.payment_status, String.Empty).Trim(), expected, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function IsStatus(order As LocalData.OrderItem, ParamArray expected As String()) As Boolean
        Return expected.Any(Function(status) String.Equals(If(order.status, String.Empty).Trim(), status, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Function ResolveOrderDate(order As LocalData.OrderItem) As DateTime
        If IsPaid(order) AndAlso order.paid_at.HasValue Then Return order.paid_at.Value
        If order.occurred_at > DateTime.MinValue Then Return order.occurred_at
        Dim parsed As DateTime
        If DateTime.TryParse(order.created_at, parsed) Then Return parsed
        Return DateTime.MinValue
    End Function

    Private Sub CmbRange_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        RefreshAnalytics()
    End Sub

    Private Sub RevenueChartCanvas_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        If analyticsLoaded Then SalesChartRenderer.Draw(RevenueChartCanvas, SalesAnalytics.BuildBuckets(orders, SelectedRange()))
    End Sub
End Class
