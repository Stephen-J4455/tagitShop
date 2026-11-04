Imports System
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class NewOrderDialog
    Inherits Window

    Public Property NewOrder As LocalData.OrderItem

        Public Sub New()
            InitializeComponent()
            CmbPaymentStatus.SelectedIndex = 0
        End Sub

        Private Sub BtnCreate_Click(sender As Object, e As RoutedEventArgs)
            Dim customer = TxtCustomerName.Text.Trim()
            If String.IsNullOrEmpty(customer) Then
                MessageBox.Show("Please enter customer name.", "New Order", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If

            Dim total As Decimal = 0
            If Not Decimal.TryParse(TxtTotalAmount.Text, total) OrElse total <= 0 Then
                MessageBox.Show("Please enter a valid total amount.", "New Order", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If

            Dim items As Integer = 1
            If Not Integer.TryParse(TxtItemCount.Text, items) OrElse items <= 0 Then
                MessageBox.Show("Please enter a valid item count.", "New Order", MessageBoxButton.OK, MessageBoxImage.Warning)
                Return
            End If

            Dim paymentStatus = DirectCast(CmbPaymentStatus.SelectedItem, ComboBoxItem).Content.ToString()
            Dim status = If(paymentStatus = "Paid", "Delivered", If(paymentStatus = "Partial", "Processing", "Pending"))

            Dim recordDebt = ChkRecordDebt.IsChecked = True
            Dim debtAmount = If(recordDebt AndAlso paymentStatus <> "Paid", total, 0D)

            NewOrder = New LocalData.OrderItem() With {
                .order_number = "ORD-" & (1000 + New Random().Next(9000)).ToString(),
                .status = status,
                .total = total,
                .created_at = "Just now",
                .seller_id = "seller-1",
                .customer_name = customer,
                .items = items,
                .recordDebt = recordDebt,
                .debtAmount = debtAmount
            }

            DialogResult = True
            Close()
        End Sub

        Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
            DialogResult = False
            Close()
        End Sub
    End Class