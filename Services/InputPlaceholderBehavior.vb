Imports System
Imports System.Windows
Imports System.Windows.Controls

Public NotInheritable Class InputPlaceholderBehavior
    Public Shared ReadOnly IsEnabledProperty As DependencyProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            GetType(Boolean),
            GetType(InputPlaceholderBehavior),
            New PropertyMetadata(False, AddressOf OnIsEnabledChanged))

    Private Sub New()
    End Sub

    Public Shared Function GetIsEnabled(element As DependencyObject) As Boolean
        Return CBool(element.GetValue(IsEnabledProperty))
    End Function

    Public Shared Sub SetIsEnabled(element As DependencyObject, value As Boolean)
        element.SetValue(IsEnabledProperty, value)
    End Sub

    Private Shared Sub OnIsEnabledChanged(element As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim textBox = TryCast(element, TextBox)
        If textBox IsNot Nothing Then
            If CBool(e.NewValue) Then
                AddHandler textBox.Loaded, AddressOf Input_Loaded
                AddHandler textBox.TextChanged, AddressOf TextBox_TextChanged
                If textBox.IsLoaded Then UpdatePlaceholder(textBox, String.IsNullOrEmpty(textBox.Text))
            Else
                RemoveHandler textBox.Loaded, AddressOf Input_Loaded
                RemoveHandler textBox.TextChanged, AddressOf TextBox_TextChanged
            End If
            Return
        End If

        Dim passwordBox = TryCast(element, PasswordBox)
        If passwordBox IsNot Nothing Then
            If CBool(e.NewValue) Then
                AddHandler passwordBox.Loaded, AddressOf Input_Loaded
                AddHandler passwordBox.PasswordChanged, AddressOf PasswordBox_PasswordChanged
                If passwordBox.IsLoaded Then UpdatePlaceholder(passwordBox, String.IsNullOrEmpty(passwordBox.Password))
            Else
                RemoveHandler passwordBox.Loaded, AddressOf Input_Loaded
                RemoveHandler passwordBox.PasswordChanged, AddressOf PasswordBox_PasswordChanged
            End If
        End If
    End Sub

    Private Shared Sub Input_Loaded(sender As Object, e As RoutedEventArgs)
        Dim textBox = TryCast(sender, TextBox)
        If textBox IsNot Nothing Then
            UpdatePlaceholder(textBox, String.IsNullOrEmpty(textBox.Text))
            Return
        End If

        Dim passwordBox = TryCast(sender, PasswordBox)
        If passwordBox IsNot Nothing Then
            UpdatePlaceholder(passwordBox, String.IsNullOrEmpty(passwordBox.Password))
        End If
    End Sub

    Private Shared Sub TextBox_TextChanged(sender As Object, e As TextChangedEventArgs)
        Dim textBox = TryCast(sender, TextBox)
        If textBox IsNot Nothing Then UpdatePlaceholder(textBox, String.IsNullOrEmpty(textBox.Text))
    End Sub

    Private Shared Sub PasswordBox_PasswordChanged(sender As Object, e As RoutedEventArgs)
        Dim passwordBox = TryCast(sender, PasswordBox)
        If passwordBox IsNot Nothing Then UpdatePlaceholder(passwordBox, String.IsNullOrEmpty(passwordBox.Password))
    End Sub

    Private Shared Sub UpdatePlaceholder(control As Control, isEmpty As Boolean)
        If control.Template Is Nothing Then Return
        control.ApplyTemplate()
        Dim placeholder = TryCast(control.Template.FindName("InputPlaceholder", control), TextBlock)
        If placeholder Is Nothing Then Return

        placeholder.Visibility = If(isEmpty AndAlso Not String.IsNullOrWhiteSpace(placeholder.Text),
                                   Visibility.Visible,
                                   Visibility.Collapsed)
    End Sub
End Class
