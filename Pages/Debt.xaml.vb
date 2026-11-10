Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Windows
Imports System.Windows.Controls
Imports Microsoft.Win32

Partial Public Class Debt
    Inherits Page

    Public Class DebtSummaryItem
        Public Property Record As LocalData.DebtRecord
        Public Property customer As String
        Public Property amount As Decimal
        Public Property paid_amount As Decimal
        Public Property balance As Decimal
        Public Property recordedOn As DateTime
        Public Property state As String
    End Class

    Public Sub New()
        InitializeComponent()
        LoadDebtData()
    End Sub

    Private Sub LoadDebtData()
        Dim records = LocalData.Load().DebtRecords
        Dim summary = records.Select(Function(record) New DebtSummaryItem() With {
            .Record = record,
            .customer = record.customer,
            .amount = record.amount,
            .paid_amount = record.paid_amount,
            .balance = Math.Max(0D, record.amount - record.paid_amount),
            .recordedOn = record.recordedOn,
            .state = If(record.amount <= record.paid_amount, "Paid", If(record.paid_amount > 0D, "Part paid", "Open"))
        }).OrderByDescending(Function(item) item.balance).ToList()

        TxtTotalDebt.Text = "GHC " & summary.Sum(Function(item) item.balance).ToString("N2")
        TxtDebtCustomers.Text = summary.Where(Function(item) item.balance > 0D).Select(Function(item) item.customer).Distinct(StringComparer.OrdinalIgnoreCase).Count().ToString()
        TxtDebtOrders.Text = summary.Where(Function(item) item.balance > 0D).Count().ToString()
        DebtGrid.ItemsSource = summary
        DebtGridContainer.Visibility = If(summary.Count = 0, Visibility.Collapsed, Visibility.Visible)
        EmptyDebtsState.Visibility = If(summary.Count = 0, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub BtnRecordDebt_Click(sender As Object, e As RoutedEventArgs)
        Dim dialog = New DebtDialog() With {.Owner = Window.GetWindow(Me)}
        If dialog.ShowDialog() <> True Then Return

        Dim records = LocalData.Load().DebtRecords
        records.Add(dialog.NewDebt)
        Try
            LocalData.Save()
        Catch ex As Exception
            records.Remove(dialog.NewDebt)
            MessageBox.Show(ex.Message, "Debt could not be saved", MessageBoxButton.OK, MessageBoxImage.Error)
            Return
        End Try

        TxtPageFeedback.Text = "Debt recorded for " & dialog.NewDebt.customer & "."
        LoadDebtData()
    End Sub

    Private Sub BtnRecordPayment_Click(sender As Object, e As RoutedEventArgs)
        Dim item = TryCast(DirectCast(sender, FrameworkElement).Tag, DebtSummaryItem)
        If item Is Nothing OrElse item.balance <= 0D Then Return
        Dim dialog = New DebtDialog(item.Record, item.balance) With {.Owner = Window.GetWindow(Me)}
        If dialog.ShowDialog() <> True Then Return

        Dim payment = New LocalData.DebtPayment() With {
            .amount = dialog.PaymentAmount,
            .paid_at = DateTime.Now,
            .note = dialog.PaymentNote
        }
        Dim previousPaidAmount = item.Record.paid_amount
        item.Record.payments.Add(payment)
        item.Record.paid_amount += dialog.PaymentAmount
        Try
            LocalData.Save()
        Catch ex As Exception
            item.Record.payments.Remove(payment)
            item.Record.paid_amount = previousPaidAmount
            MessageBox.Show(ex.Message, "Payment could not be saved", MessageBoxButton.OK, MessageBoxImage.Error)
            Return
        End Try

        TxtPageFeedback.Text = "Payment of GHC " & dialog.PaymentAmount.ToString("N2") & " recorded for " & item.customer & "."
        LoadDebtData()
    End Sub

    Private Sub BtnMarkPaid_Click(sender As Object, e As RoutedEventArgs)
        Dim item = TryCast(DirectCast(sender, FrameworkElement).Tag, DebtSummaryItem)
        If item Is Nothing OrElse item.balance <= 0D Then Return

        Dim prompt = "Record the remaining GHC " & item.balance.ToString("N2") & " as paid by " & item.customer & "?"
        If MessageBox.Show(prompt, "Mark debt as paid", MessageBoxButton.YesNo, MessageBoxImage.Question) <> MessageBoxResult.Yes Then Return

        Dim payment = New LocalData.DebtPayment() With {
            .amount = item.balance,
            .paid_at = DateTime.Now,
            .note = "Marked as paid"
        }
        Dim previousPaidAmount = item.Record.paid_amount
        item.Record.payments.Add(payment)
        item.Record.paid_amount += payment.amount
        Try
            LocalData.Save()
        Catch ex As Exception
            item.Record.payments.Remove(payment)
            item.Record.paid_amount = previousPaidAmount
            MessageBox.Show(ex.Message, "Debt could not be marked paid", MessageBoxButton.OK, MessageBoxImage.Error)
            Return
        End Try

        TxtPageFeedback.Text = "Debt for " & item.customer & " marked as paid."
        LoadDebtData()
    End Sub

    Private Sub BtnPaymentHistory_Click(sender As Object, e As RoutedEventArgs)
        Dim item = TryCast(DirectCast(sender, FrameworkElement).Tag, DebtSummaryItem)
        If item Is Nothing Then Return

        Dim dialog = New DebtHistoryDialog(item.Record) With {.Owner = Window.GetWindow(Me)}
        dialog.ShowDialog()
    End Sub

    Private Sub BtnExport_Click(sender As Object, e As RoutedEventArgs)
        Dim records = LocalData.Load().DebtRecords
        If records.Count = 0 Then
            MessageBox.Show("There are no debt records to export.", "Export debt", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim dialog = New SaveFileDialog() With {
            .Title = "Export debt records",
            .Filter = "Excel workbook (*.xlsx)|*.xlsx",
            .DefaultExt = ".xlsx",
            .AddExtension = True,
            .OverwritePrompt = True,
            .FileName = "debt-records-" & DateTime.Today.ToString("yyyy-MM-dd") & ".xlsx"
        }
        If dialog.ShowDialog(Window.GetWindow(Me)) <> True Then Return

        Try
            ExcelExportService.ExportDebt(dialog.FileName, records)
            TxtPageFeedback.Text = records.Count.ToString("N0") & " debt records exported."
            MessageBox.Show(records.Count.ToString("N0") & " debt records exported successfully.", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information)
        Catch ex As Exception
            MessageBox.Show("Debt records could not be exported. " & ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub
End Class