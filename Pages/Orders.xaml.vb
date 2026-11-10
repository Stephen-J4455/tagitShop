Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports Microsoft.Win32

Public Class SalesOrderRow
    Public Property OrderNumber As String
    Public Property CustomerName As String
    Public Property ItemCount As Integer
    Public Property PaymentLabel As String
    Public Property PaymentState As String
    Public Property DateLabel As String
    Public Property Total As Decimal
End Class

Partial Public Class Orders
    Inherits Page

    Private orders As New List(Of LocalData.OrderItem)()
    Private orderRows As New List(Of SalesOrderRow)()

    Public Sub New()
        InitializeComponent()
        AddHandler TxtSearch.TextChanged, AddressOf TxtSearch_TextChanged
        AddHandler CmbPaymentFilter.SelectionChanged, AddressOf CmbPaymentFilter_SelectionChanged
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Await RefreshOrdersAsync()
    End Sub

    Private Async Function RefreshOrdersAsync() As Threading.Tasks.Task
        LoadingIndicator.Visibility = Visibility.Visible
        EmptySalesState.Visibility = Visibility.Collapsed
        OrderGrid.Visibility = Visibility.Collapsed
        Try
            Await DataCache.EnsureLoadedAsync()
            orders = DataCache.Orders.ToList()
            Dim paid = orders.Where(Function(order) String.Equals(order.payment_status, "success", StringComparison.OrdinalIgnoreCase)).ToList()
            Dim customerCount = paid.
                Select(Function(order) If(order.customer_name, String.Empty).Trim()).
                Where(Function(name) Not String.IsNullOrWhiteSpace(name) AndAlso Not String.Equals(name, "Customer", StringComparison.OrdinalIgnoreCase)).
                Distinct(StringComparer.OrdinalIgnoreCase).
                Count()
            TxtTotalSales.Text = paid.Count.ToString("N0")
            TxtTotalRevenue.Text = "GHC " & SalesAnalytics.PaidRevenue(paid).ToString("N2")
            TxtCustomerCount.Text = customerCount.ToString("N0")
            TxtUnpaidCount.Text = orders.Where(Function(order) Not String.Equals(order.payment_status, "success", StringComparison.OrdinalIgnoreCase)).Count().ToString("N0")
            orderRows = orders.
                OrderByDescending(Function(order) ResolveOrderDate(order)).
                Select(Function(order) CreateSalesOrderRow(order)).
                ToList()
            TxtStatus.Text = orders.Count.ToString("N0") & " orders / GHC " & SalesAnalytics.PaidRevenue(paid).ToString("N2") & " paid revenue"
            ApplySalesFilters()
        Catch ex As Exception
            TxtStatus.Text = ex.Message
            orderRows = New List(Of SalesOrderRow)()
            ApplySalesFilters()
            TxtEmptySalesTitle.Text = "Sales could not be loaded"
            TxtEmptySalesMessage.Text = ex.Message
        Finally
            LoadingIndicator.Visibility = Visibility.Collapsed
        End Try
    End Function

    Private Function CreateSalesOrderRow(order As LocalData.OrderItem) As SalesOrderRow
        Dim paymentStatus = If(order.payment_status, String.Empty).Trim()
        Dim paymentLabel = If(String.Equals(paymentStatus, "success", StringComparison.OrdinalIgnoreCase),
                              "Paid",
                              If(String.IsNullOrWhiteSpace(paymentStatus), "Unknown", ToDisplayLabel(paymentStatus)))
        Dim dateValue = ResolveOrderDate(order)

        Return New SalesOrderRow() With {
            .OrderNumber = If(String.IsNullOrWhiteSpace(order.order_number), "—", order.order_number),
            .CustomerName = If(String.IsNullOrWhiteSpace(order.customer_name), "Customer", order.customer_name.Trim()),
            .ItemCount = order.items,
            .PaymentLabel = paymentLabel,
            .PaymentState = If(String.Equals(paymentStatus, "success", StringComparison.OrdinalIgnoreCase), "paid", "unpaid"),
            .DateLabel = If(dateValue = DateTime.MinValue, "—", dateValue.ToString("MMM d, yyyy  h:mm tt")),
            .Total = order.total
        }
    End Function

    Private Function ToDisplayLabel(value As String) As String
        Dim normalized = value.Replace("_", " ").Replace("-", " ").Trim().ToLowerInvariant()
        If normalized.Length = 0 Then Return "Unknown"
        Return Char.ToUpperInvariant(normalized(0)) & normalized.Substring(1)
    End Function

    Private Sub ApplySalesFilters()
        Dim query = TxtSearch.Text.Trim()
        Dim selectedFilter = TryCast(CmbPaymentFilter.SelectedItem, ComboBoxItem)
        Dim paymentFilter = If(selectedFilter Is Nothing OrElse selectedFilter.Tag Is Nothing, "all", selectedFilter.Tag.ToString())

        Dim visibleRows = orderRows.Where(
            Function(row)
                Dim matchesPayment =
                    paymentFilter = "all" OrElse
                    (paymentFilter = "paid" AndAlso row.PaymentState = "paid") OrElse
                    (paymentFilter = "unpaid" AndAlso row.PaymentState <> "paid")
                Dim matchesSearch =
                    String.IsNullOrWhiteSpace(query) OrElse
                    row.OrderNumber.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                    row.CustomerName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                    row.PaymentLabel.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                Return matchesPayment AndAlso matchesSearch
            End Function).ToList()

        OrderGrid.ItemsSource = visibleRows
        OrderGrid.Visibility = If(visibleRows.Count > 0, Visibility.Visible, Visibility.Collapsed)
        EmptySalesState.Visibility = If(visibleRows.Count = 0, Visibility.Visible, Visibility.Collapsed)
        TxtResultCount.Text = visibleRows.Count.ToString("N0") & " of " & orderRows.Count.ToString("N0") & " orders"

        If orderRows.Count = 0 Then
            TxtEmptySalesTitle.Text = "No sales yet"
            TxtEmptySalesMessage.Text = "Orders will appear here when they are recorded."
        ElseIf visibleRows.Count = 0 Then
            TxtEmptySalesTitle.Text = "No matching orders"
            TxtEmptySalesMessage.Text = "Try another search or payment filter."
        End If
    End Sub

    Private Sub TxtSearch_TextChanged(sender As Object, e As TextChangedEventArgs)
        ApplySalesFilters()
    End Sub

    Private Sub CmbPaymentFilter_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        ApplySalesFilters()
    End Sub

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

    Private Sub BtnExport_Click(sender As Object, e As RoutedEventArgs)
        If orders.Count = 0 Then
            MessageBox.Show("There are no sales records to export.", "Export sales", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim dialog = New SaveFileDialog() With {
            .Title = "Export sales records",
            .Filter = "Excel workbook (*.xlsx)|*.xlsx",
            .DefaultExt = ".xlsx",
            .AddExtension = True,
            .OverwritePrompt = True,
            .FileName = "sales-records-" & DateTime.Today.ToString("yyyy-MM-dd") & ".xlsx"
        }
        If dialog.ShowDialog(Window.GetWindow(Me)) <> True Then Return

        Try
            ExcelExportService.ExportSales(dialog.FileName, orders)
            TxtStatus.Text = orders.Count.ToString("N0") & " sales records exported."
            MessageBox.Show(orders.Count.ToString("N0") & " sales records exported successfully.", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information)
        Catch ex As Exception
            MessageBox.Show("Sales records could not be exported. " & ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub
End Class