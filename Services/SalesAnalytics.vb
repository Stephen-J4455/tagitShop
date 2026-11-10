Imports System
Imports System.Collections.Generic
Imports System.Linq

Public Enum SalesRange
    Daily
    Monthly
    Yearly
    Weekly
    CurrentMonth
    SixMonths
    CurrentYear
End Enum

Public Class SalesBucket
    Public Property Start As DateTime
    Public Property Label As String
    Public Property Revenue As Decimal
    Public Property GrossProfit As Decimal?
    Public Property OrderCount As Integer
    Public ReadOnly Property GrossProfitDisplay As String
        Get
            Return If(GrossProfit.HasValue, GrossProfit.Value.ToString("N2"), "Unavailable")
        End Get
    End Property
End Class

Public NotInheritable Class SalesAnalytics
    Private Sub New()
    End Sub

    Public Shared Function BuildBuckets(orders As IEnumerable(Of LocalData.OrderItem), range As SalesRange, Optional referenceDate As DateTime? = Nothing) As List(Of SalesBucket)
        Dim today = If(referenceDate, DateTime.Today).Date
        Dim buckets As New List(Of SalesBucket)()

        Select Case range
            Case SalesRange.Daily
                For offset = 6 To 0 Step -1
                    Dim dateValue = today.AddDays(-offset)
                    buckets.Add(New SalesBucket() With {.Start = dateValue, .Label = dateValue.ToString("ddd"), .GrossProfit = 0D})
                Next
            Case SalesRange.Weekly
                For offset = 6 To 0 Step -1
                    Dim dateValue = today.AddDays(-offset)
                    buckets.Add(New SalesBucket() With {.Start = dateValue, .Label = dateValue.ToString("ddd"), .GrossProfit = 0D})
                Next
            Case SalesRange.CurrentMonth
                For offset = today.Day - 1 To 0 Step -1
                    Dim dateValue = today.AddDays(-offset)
                    buckets.Add(New SalesBucket() With {.Start = dateValue, .Label = dateValue.ToString("d"), .GrossProfit = 0D})
                Next
            Case SalesRange.Monthly
                Dim currentMonth = New DateTime(today.Year, today.Month, 1)
                For offset = 11 To 0 Step -1
                    Dim dateValue = currentMonth.AddMonths(-offset)
                    buckets.Add(New SalesBucket() With {.Start = dateValue, .Label = dateValue.ToString("MMM"), .GrossProfit = 0D})
                Next
            Case SalesRange.SixMonths
                Dim currentMonth = New DateTime(today.Year, today.Month, 1)
                For offset = 5 To 0 Step -1
                    Dim dateValue = currentMonth.AddMonths(-offset)
                    buckets.Add(New SalesBucket() With {.Start = dateValue, .Label = dateValue.ToString("MMM"), .GrossProfit = 0D})
                Next
            Case SalesRange.CurrentYear
                For monthIndex = 1 To today.Month
                    Dim dateValue = New DateTime(today.Year, monthIndex, 1)
                    buckets.Add(New SalesBucket() With {.Start = dateValue, .Label = dateValue.ToString("MMM"), .GrossProfit = 0D})
                Next
            Case SalesRange.Yearly
                For offset = 4 To 0 Step -1
                    Dim dateValue = New DateTime(today.Year - offset, 1, 1)
                    buckets.Add(New SalesBucket() With {.Start = dateValue, .Label = dateValue.ToString("yyyy"), .GrossProfit = 0D})
                Next
        End Select

        For Each order In orders
            If Not String.Equals(order.payment_status, "success", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim saleDate = ResolvePaidDate(order)
            If Not saleDate.HasValue Then Continue For
            Dim bucket = buckets.FirstOrDefault(Function(candidate) Contains(candidate.Start, saleDate.Value, range))
            If bucket Is Nothing Then Continue For
            bucket.Revenue += order.total
            bucket.OrderCount += 1
            If order.cost_of_goods_sold.HasValue AndAlso bucket.GrossProfit.HasValue Then
                bucket.GrossProfit += order.total - order.cost_of_goods_sold.Value
            Else
                bucket.GrossProfit = Nothing
            End If
        Next

        Return buckets
    End Function

    Public Shared Function PaidRevenue(orders As IEnumerable(Of LocalData.OrderItem)) As Decimal
        Return orders.Where(Function(order) String.Equals(order.payment_status, "success", StringComparison.OrdinalIgnoreCase)).Sum(Function(order) order.total)
    End Function

    Public Shared Function PaidOrderCount(orders As IEnumerable(Of LocalData.OrderItem)) As Integer
        Return orders.Where(Function(order) String.Equals(order.payment_status, "success", StringComparison.OrdinalIgnoreCase)).Count()
    End Function

    Public Shared Function PaidGrossProfit(orders As IEnumerable(Of LocalData.OrderItem)) As Decimal?
        Dim paidOrders = orders.Where(Function(order) String.Equals(order.payment_status, "success", StringComparison.OrdinalIgnoreCase)).ToList()
        If paidOrders.Count = 0 Then Return 0D
        If paidOrders.Any(Function(order) Not order.cost_of_goods_sold.HasValue) Then Return Nothing
        Return paidOrders.Sum(Function(order) order.total - order.cost_of_goods_sold.Value)
    End Function

    Private Shared Function ResolvePaidDate(order As LocalData.OrderItem) As DateTime?
        If order.paid_at.HasValue Then Return order.paid_at.Value
        If order.occurred_at > DateTime.MinValue Then Return order.occurred_at
        Dim parsed As DateTime
        If DateTime.TryParse(order.created_at, parsed) Then Return parsed
        Return Nothing
    End Function

    Private Shared Function Contains(bucketStart As DateTime, saleDate As DateTime, range As SalesRange) As Boolean
        Select Case range
            Case SalesRange.Daily
                Return saleDate.Date = bucketStart.Date
            Case SalesRange.Weekly, SalesRange.CurrentMonth
                Return saleDate.Date = bucketStart.Date
            Case SalesRange.Monthly
                Return saleDate.Year = bucketStart.Year AndAlso saleDate.Month = bucketStart.Month
            Case SalesRange.SixMonths, SalesRange.CurrentYear
                Return saleDate.Year = bucketStart.Year AndAlso saleDate.Month = bucketStart.Month
            Case SalesRange.Yearly
                Return saleDate.Year = bucketStart.Year
            Case Else
                Return False
        End Select
    End Function
End Class