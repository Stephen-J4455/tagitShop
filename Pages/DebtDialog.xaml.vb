Imports System
Imports System.Windows

Partial Public Class DebtDialog
    Inherits Window

    Private ReadOnly isPayment As Boolean
    Private ReadOnly outstandingBalance As Decimal
    Public Property NewDebt As LocalData.DebtRecord
    Public Property PaymentAmount As Decimal
    Public Property PaymentNote As String

    Public Sub New(Optional existingDebt As LocalData.DebtRecord = Nothing, Optional balance As Decimal = 0D)
        InitializeComponent()
        isPayment = existingDebt IsNot Nothing
        outstandingBalance = balance
        If Not isPayment Then Return

        TxtTitle.Text = "Record payment"
        TxtDescription.Text = "Outstanding balance: GHC " & balance.ToString("N2")
        TxtCustomer.Text = existingDebt.customer
        TxtCustomer.IsReadOnly = True
        TxtAmountLabel.Text = "PAYMENT AMOUNT (GHC)"
        TxtAmount.Text = balance.ToString("0.00")
        TxtNote.Text = String.Empty
        BtnSave.Content = "Add payment"
    End Sub

    Private Sub BtnSave_Click(sender As Object, e As RoutedEventArgs)
        Dim amount As Decimal
        If Not Decimal.TryParse(TxtAmount.Text, amount) OrElse amount <= 0D Then
            MessageBox.Show("Enter an amount greater than zero.", "Customer debt", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        If isPayment Then
            If amount > outstandingBalance Then
                MessageBox.Show("Payment cannot exceed the outstanding balance.", "Customer debt", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If
            PaymentAmount = amount
            PaymentNote = TxtNote.Text.Trim()
        Else
            Dim customer = TxtCustomer.Text.Trim()
            If String.IsNullOrWhiteSpace(customer) Then
                MessageBox.Show("Enter the customer's name.", "Customer debt", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If
            NewDebt = New LocalData.DebtRecord() With {
                .id = Guid.NewGuid().ToString("N"),
                .customer = customer,
                .amount = amount,
                .paid_amount = 0D,
                .recordedOn = DateTime.Now,
                .notes = TxtNote.Text.Trim()
            }
        End If

        DialogResult = True
        Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub
End Class