Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Security
    Inherits Page

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Async Sub BtnChange_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show("Password changed.", "Security", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub
End Class