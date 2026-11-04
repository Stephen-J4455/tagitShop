Imports System.Windows
Imports System.Windows.Controls

Partial Public Class PaymentAccount
    Inherits Page

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Async Sub BtnSave_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show("Payment account saved.", "Payment Account", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub
End Class