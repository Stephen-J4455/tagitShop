Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Orders
    Inherits Page

    Private orders As New List(Of LocalData.OrderItem)()

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Await RefreshOrdersAsync()
    End Sub

    Private Async Function RefreshOrdersAsync() As Threading.Tasks.Task
        Try
            orders = Await SupabaseService.LoadOrdersAsync()
            OrderGrid.ItemsSource = orders.OrderByDescending(Function(order) ResolveOrderDate(order)).ToList()
            Dim paid = orders.Where(Function(order) String.Equals(order.payment_status, "success", StringComparison.OrdinalIgnoreCase)).ToList()
            Dim customerCount = paid.
                Select(Function(order) If(order.customer_name, String.Empty).Trim()).
                Where(Function(name) Not String.IsNullOrWhiteSpace(name) AndAlso Not String.Equals(name, "Customer", StringComparison.OrdinalIgnoreCase)).
                Distinct(StringComparer.OrdinalIgnoreCase).
                Count()
            TxtTotalSales.Text = paid.Count.ToString("N0")
            TxtTotalRevenue.Text = "GHC " & SalesAnalytics.PaidRevenue(paid).ToString("N2")
            TxtCustomerCount.Text = customerCount.ToString("N0")
            TxtStatus.Text = orders.Count.ToString() & " records / GHC " & SalesAnalytics.PaidRevenue(paid).ToString("N2") & " paid revenue"
        Catch ex As Exception
            TxtStatus.Text = ex.Message
            OrderGrid.ItemsSource = New List(Of LocalData.OrderItem)()
        End Try
    End Function

    Private Function ResolveOrderDate(order As LocalData.OrderItem) As DateTime
        If order.paid_at.HasValue Then Return order.paid_at.Value
        If order.occurred_at > DateTime.MinValue Then Return order.occurred_at
        Dim parsed As DateTime
        If DateTime.TryParse(order.created_at, parsed) Then Return parsed
        Return DateTime.MinValue
    End Function

    Private Async Sub BtnRefresh_Click(sender As Object, e As RoutedEventArgs)
        Await RefreshOrdersAsync()
    End Sub
End Class