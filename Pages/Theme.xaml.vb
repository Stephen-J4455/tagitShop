Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Theme
    Inherits Page

    Public Sub New()
        InitializeComponent()
    End Sub

    Private Async Sub BtnApply_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show("Theme applied.", "Theme", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub
End Class