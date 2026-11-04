Imports System
Imports System.Linq
Imports System.Windows

Partial Public Class DebtHistoryDialog
    Inherits Window

    Public Sub New(record As LocalData.DebtRecord)
        InitializeComponent()

        TxtCustomer.Text = record.customer & " · recorded " & record.recordedOn.ToString("MMM d, yyyy")
        TxtOriginalAmount.Text = "GHC " & record.amount.ToString("N2")
        TxtPaidAmount.Text = "GHC " & record.paid_amount.ToString("N2")
        TxtBalance.Text = "GHC " & Math.Max(0D, record.amount - record.paid_amount).ToString("N2")
        PaymentGrid.ItemsSource = record.payments.
            OrderByDescending(Function(payment) payment.paid_at).
            ToList()
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As RoutedEventArgs)
        Close()
    End Sub
End Class
