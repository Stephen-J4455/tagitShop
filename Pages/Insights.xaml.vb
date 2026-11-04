Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Insights
    Inherits Page

    Private orders As New List(Of LocalData.OrderItem)()
    Private analyticsLoaded As Boolean

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Try
            orders = Await SupabaseService.LoadOrdersAsync()
            analyticsLoaded = True
            RefreshAnalytics()
            TxtInsightsFeedback.Text = If(AppSession.Mode = StoreMode.Live,
                "Live orders are grouped by paid date. Gross profit requires item cost data, which is not included in the live order feed.",
                "Demo sales include sample cost data and cover each of the last seven days.")
        Catch ex As Exception
            TxtInsightsFeedback.Text = ex.Message
        End Try
    End Sub

    Private Function SelectedRange() As SalesRange
        Dim selected = TryCast(CmbRange.SelectedItem, ComboBoxItem)
        If selected Is Nothing Then Return SalesRange.Daily
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
        Dim buckets = SalesAnalytics.BuildBuckets(orders, range)
        Dim revenue = buckets.Sum(Function(bucket) bucket.Revenue)
        Dim paidOrders = buckets.Sum(Function(bucket) bucket.OrderCount)
        Dim grossProfit As Decimal? = 0D
        For Each bucket In buckets
            If bucket.OrderCount > 0 AndAlso Not bucket.GrossProfit.HasValue Then
                grossProfit = Nothing
                Exit For
            End If
            If grossProfit.HasValue Then grossProfit += bucket.GrossProfit.GetValueOrDefault()
        Next
        TxtTotalRevenue.Text = "GHC " & revenue.ToString("N2")
        TxtGrossProfit.Text = If(grossProfit.HasValue, "GHC " & grossProfit.Value.ToString("N2"), "Unavailable")
        TxtTotalOrders.Text = paidOrders.ToString()
        TxtAvgOrder.Text = "GHC " & If(paidOrders = 0, 0D, revenue / paidOrders).ToString("N2")
        TxtChartTitle.Text = If(range = SalesRange.Daily, "Revenue and gross profit / last 7 days", If(range = SalesRange.Monthly, "Revenue and gross profit / last 12 months", "Revenue and gross profit / last 5 years"))
        BucketsGrid.ItemsSource = buckets
        SalesChartRenderer.Draw(RevenueChartCanvas, buckets)
    End Sub

    Private Sub CmbRange_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        RefreshAnalytics()
    End Sub

    Private Sub RevenueChartCanvas_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        If analyticsLoaded Then SalesChartRenderer.Draw(RevenueChartCanvas, SalesAnalytics.BuildBuckets(orders, SelectedRange()))
    End Sub
End Class