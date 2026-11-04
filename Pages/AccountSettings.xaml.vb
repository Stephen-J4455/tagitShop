Imports System.Windows
Imports System.Windows.Controls

Partial Public Class AccountSettings
    Inherits Page

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Async Sub BtnSave_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show("Account settings saved.", "Account Settings", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub
End Class