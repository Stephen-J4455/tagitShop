Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls

Public Class ActiveFlashSaleCard
    Public Property Title As String
    Public Property ImageUrl As String
    Public Property DiscountLabel As String
    Public Property FlashPriceLabel As String
    Public Property OriginalPriceLabel As String
    Public Property TimeLeftLabel As String
    Public Property EndTimeLabel As String
    Public Property QuantityLabel As String
    Public Property AvailabilityLabel As String
    Public Property QuantityProgress As Double
    Public Property HasQuantityLimit As Boolean
    Public Property EndTime As DateTimeOffset
End Class

Partial Public Class Flash
    Inherits Page

    Public Sub New()
        InitializeComponent()
        AddHandler Loaded, AddressOf Page_Loaded
    End Sub

    Private Async Sub Page_Loaded(sender As Object, e As RoutedEventArgs)
        RemoveHandler Loaded, AddressOf Page_Loaded
        Await LoadFlashSalesAsync()
    End Sub

    Private Async Function LoadFlashSalesAsync() As System.Threading.Tasks.Task
        Try
            LoadingIndicator.Visibility = Visibility.Visible
            EmptyFlashState.Visibility = Visibility.Collapsed
            ActiveSalesSummary.Visibility = Visibility.Collapsed
            FlashGrid.Visibility = Visibility.Collapsed

            Await DataCache.EnsureLoadedAsync()
            Dim now = DateTimeOffset.Now
            Dim activeSales = DataCache.FlashSales.
                Select(Function(sale) CreateActiveCard(sale, now)).
                Where(Function(card) card IsNot Nothing).
                OrderBy(Function(card) card.EndTime).
                ToList()

            FlashGrid.ItemsSource = activeSales
            Dim hasActiveSales = activeSales.Count > 0
            FlashGrid.Visibility = If(hasActiveSales, Visibility.Visible, Visibility.Collapsed)
            ActiveSalesSummary.Visibility = If(hasActiveSales, Visibility.Visible, Visibility.Collapsed)
            EmptyFlashState.Visibility = If(hasActiveSales, Visibility.Collapsed, Visibility.Visible)
            TxtActiveSummary.Text = activeSales.Count.ToString("N0") & If(activeSales.Count = 1, " active offer", " active offers")
            TxtActiveCount.Text = activeSales.Count.ToString("N0")
            If hasActiveSales Then TxtNextEnding.Text = activeSales(0).TimeLeftLabel

            TxtStatus.Text = If(AppSession.Mode = StoreMode.Live,
                "Showing active flash sales from your store.",
                "Showing active demo flash sales.")
        Catch ex As Exception
            FlashGrid.ItemsSource = New List(Of ActiveFlashSaleCard)()
            FlashGrid.Visibility = Visibility.Collapsed
            ActiveSalesSummary.Visibility = Visibility.Collapsed
            EmptyFlashState.Visibility = Visibility.Visible
            TxtEmptyTitle.Text = "Flash sales could not be loaded"
            TxtEmptyMessage.Text = ex.Message
            TxtStatus.Text = ex.Message
        Finally
            LoadingIndicator.Visibility = Visibility.Collapsed
        End Try
    End Function

    Private Function CreateActiveCard(sale As SupabaseService.FlashSaleItem, now As DateTimeOffset) As ActiveFlashSaleCard
        If sale Is Nothing OrElse Not sale.is_active Then Return Nothing

        Dim startTime As DateTimeOffset
        Dim endTime As DateTimeOffset
        If Not DateTimeOffset.TryParse(sale.start_time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, startTime) OrElse
           Not DateTimeOffset.TryParse(sale.end_time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, endTime) Then
            Return Nothing
        End If
        If startTime > now OrElse endTime <= now Then Return Nothing

        Dim hasLimit = sale.max_quantity.HasValue AndAlso sale.max_quantity.Value > 0
        If hasLimit AndAlso sale.sold_quantity >= sale.max_quantity.Value Then Return Nothing

        Dim progress As Double = 0
        Dim availability As String = "No quantity cap"
        If hasLimit Then
            Dim remaining = Math.Max(0, sale.max_quantity.Value - sale.sold_quantity)
            progress = Math.Min(100.0R, Math.Max(0.0R, sale.sold_quantity * 100.0R / sale.max_quantity.Value))
            availability = remaining.ToString("N0") & " left"
        End If

        Return New ActiveFlashSaleCard() With {
            .Title = If(String.IsNullOrWhiteSpace(sale.title), "Flash sale", sale.title),
            .ImageUrl = sale.image_url,
            .DiscountLabel = sale.discount.ToString("N0") & "% off",
            .FlashPriceLabel = "GHC " & sale.flash_price.ToString("N2"),
            .OriginalPriceLabel = "GHC " & sale.original_price.ToString("N2"),
            .TimeLeftLabel = "Ends in " & FormatTimeRemaining(endTime - now),
            .EndTimeLabel = endTime.ToLocalTime().ToString("MMM d, h:mm tt"),
            .QuantityLabel = sale.sold_quantity.ToString("N0") & If(sale.sold_quantity = 1, " unit sold", " units sold"),
            .AvailabilityLabel = availability,
            .QuantityProgress = progress,
            .HasQuantityLimit = hasLimit,
            .EndTime = endTime
        }
    End Function

    Private Function FormatTimeRemaining(remaining As TimeSpan) As String
        If remaining.TotalDays >= 1 Then
            Return CInt(Math.Ceiling(remaining.TotalDays)).ToString() & If(remaining.TotalDays < 2, " day", " days")
        End If
        If remaining.TotalHours >= 1 Then
            Return CInt(Math.Ceiling(remaining.TotalHours)).ToString() & If(remaining.TotalHours < 2, " hour", " hours")
        End If
        Return Math.Max(1, CInt(Math.Ceiling(remaining.TotalMinutes))).ToString() & " min"
    End Function

    Private Async Sub BtnCreateFlash_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New FlashSaleDialog() With {.Owner = Window.GetWindow(Me)}
        If dialog.ShowDialog() <> True Then Return
        Try
            Dim saleStartTime = dialog.StartTime
            Dim saleEndTime = dialog.EndTime
            If saleEndTime <= saleStartTime Then
                Throw New InvalidOperationException("The flash sale end time must be after its start time.")
            End If
            Await SupabaseService.CreateFlashSaleAsync(dialog.ProductId, dialog.FlashPrice, saleStartTime, saleEndTime, dialog.MaxQuantity)
            DataCache.FlashSales = Await SupabaseService.LoadFlashSalesAsync()
            Await LoadFlashSalesAsync()
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Flash sale could not be created", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub
End Class
