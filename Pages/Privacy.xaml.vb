Imports System
Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Privacy
    Inherits Page

    Public Class PrivacyItem
        Public Property title As String
        Public Property description As String
    End Class

    Public Sub New()
        InitializeComponent()
        LoadLocalPrivacy()
    End Sub

    Private Sub LoadLocalPrivacy()
        Try
            Dim items = New List(Of PrivacyItem)() From {
                New PrivacyItem() With {
                    .title = "Data Collection",
                    .description = "We collect store name, email, and order data to operate your shop."
                },
                New PrivacyItem() With {
                    .title = "Customer Data",
                    .description = "Customer messages and order details are stored locally on your device."
                },
                New PrivacyItem() With {
                    .title = "Data Sharing",
                    .description = "We do not share your store data with third parties."
                },
                New PrivacyItem() With {
                    .title = "Account Security",
                    .description = "Your login credentials are validated locally using demo accounts."
                }
            }

            PrivacyGrid.ItemsSource = items
            TxtStatus.Text = $"Loaded {items.Count} privacy settings (local)"
        Catch ex As Exception
            TxtStatus.Text = "Privacy load error: " & ex.Message
        End Try
    End Sub
End Class
