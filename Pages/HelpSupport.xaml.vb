Imports System
Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class HelpSupport
    Inherits Page

    Public Class FaqItem
        Public Property question As String
        Public Property answer As String
    End Class

    Public Sub New()
        InitializeComponent()
        LoadLocalFaqs()
    End Sub

    Private Sub LoadLocalFaqs()
        Try
            Dim faqs = New List(Of FaqItem)() From {
                New FaqItem() With {
                    .question = "How do I add a product to my catalog?",
                    .answer = "Go to the Catalog page and click '+ Add Product' to create a new product listing."
                },
                New FaqItem() With {
                    .question = "How do I create a flash sale?",
                    .answer = "Navigate to the Flash Sales page and click '+ Create Flash Sale' to set up a limited-time promotion."
                },
                New FaqItem() With {
                    .question = "How do I post a status update?",
                    .answer = "Go to the Statuses page and click '+ Post Status' to share an update with your customers."
                },
                New FaqItem() With {
                    .question = "How do I view my orders?",
                    .answer = "The Orders page shows all customer orders with status, total, and customer details."
                },
                New FaqItem() With {
                    .question = "How do I update my store profile?",
                    .answer = "Visit the Store Profile page to edit your store name, email, and description."
                },
                New FaqItem() With {
                    .question = "How do I create a discount coupon?",
                    .answer = "Go to the Coupons page and click '+ Create Coupon' to generate a new promo code."
                }
            }

            FaqGrid.ItemsSource = faqs
            TxtStatus.Text = $"Loaded {faqs.Count} help articles (local)"
        Catch ex As Exception
            TxtStatus.Text = "Help load error: " & ex.Message
        End Try
    End Sub
End Class
