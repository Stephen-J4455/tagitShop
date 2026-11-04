Imports System
Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Messages
    Inherits Page

    Public Class MessageItem
        Public Property customer As String
        Public Property message As String
        Public Property time As String
    End Class

    Public Sub New()
        InitializeComponent()
        LoadLocalMessages()
    End Sub

    Private Sub LoadLocalMessages()
        Try
            Dim messages = New List(Of MessageItem)() From {
                New MessageItem() With {.customer = "Alice Johnson", .message = "Hi, when will the organic apples be back in stock?", .time = "10 min ago"},
                New MessageItem() With {.customer = "Bob Smith", .message = "Do you deliver to Lagos Island?", .time = "1 hour ago"},
                New MessageItem() With {.customer = "Carol Williams", .message = "Can I get a discount on bulk bread order?", .time = "3 hours ago"},
                New MessageItem() With {.customer = "David Brown", .message = "My order #ORD-1023 hasn't arrived yet.", .time = "5 hours ago"},
                New MessageItem() With {.customer = "Eve Davis", .message = "Great products! Love the fresh produce.", .time = "1 day ago"}
            }

            MessagesGrid.ItemsSource = messages
            TxtStatus.Text = $"Loaded {messages.Count} messages (local)"
        Catch ex As Exception
            TxtStatus.Text = "Messages load error: " & ex.Message
        End Try
    End Sub
End Class
