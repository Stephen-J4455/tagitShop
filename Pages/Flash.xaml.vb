Imports System
Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Controls

Partial Public Class Flash
    Inherits Page

    Public Class FlashSaleItem
        Public Property title As String
        Public Property discount As Integer
        Public Property description As String
        Public Property product As String
        Public Property endTime As String
        Public Property progress As Double
    End Class

    Public Sub New()
        InitializeComponent()
        LoadLocalFlashSales()
    End Sub

    Private Sub LoadLocalFlashSales()
        Try
            Dim sales = New List(Of FlashSaleItem)() From {
                New FlashSaleItem() With {
                    .title = "Weekend Blowout",
                    .discount = 25,
                    .description = "Everything must go! Huge discounts on all categories.",
                    .product = "All Products",
                    .endTime = "Sun, 11:59 PM",
                    .progress = 65
                },
                New FlashSaleItem() With {
                    .title = "Fresh Produce Deal",
                    .discount = 20,
                    .description = "Fresh fruits and vegetables at unbeatable prices.",
                    .product = "Organic Apples, Spinach",
                    .endTime = "Sat, 6:00 PM",
                    .progress = 80
                },
                New FlashSaleItem() With {
                    .title = "Bakery Special",
                    .discount = 15,
                    .description = "Freshly baked bread and pastries.",
                    .product = "Whole Grain Bread, Croissants",
                    .endTime = "Fri, 9:00 PM",
                    .progress = 90
                }
            }

            FlashGrid.ItemsSource = sales
            TxtStatus.Text = $"Loaded {sales.Count} flash sales (local)"
        Catch ex As Exception
            TxtStatus.Text = "Flash sales load error: " & ex.Message
        End Try
    End Sub

    Private Sub BtnCreateFlash_Click(sender As Object, e As RoutedEventArgs)
        MessageBox.Show("Create a new flash sale with discount, products, and end time.", "Create Flash Sale", MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub
End Class
