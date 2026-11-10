Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text.RegularExpressions
Imports System.Windows.Media
Imports System.Windows.Controls
Imports System.Windows.Documents

Public NotInheritable Class MarkdownConverter
    Private Sub New()
    End Sub

    Private Shared Function GetTextColor() As Color
        If ThemeManager.IsDark Then
            Return Color.FromRgb(243, 244, 246)
        End If
        Return Color.FromRgb(32, 32, 32)
    End Function

    Private Shared Function GetMutedColor() As Color
        If ThemeManager.IsDark Then
            Return Color.FromRgb(165, 176, 192)
        End If
        Return Color.FromRgb(114, 114, 114)
    End Function

    Private Shared Function GetCodeBackground() As Brush
        If ThemeManager.IsDark Then
            Return New SolidColorBrush(Color.FromRgb(27, 36, 50))
        End If
        Return Brushes.LightGray
    End Function

    Private Shared Function GetBorderColor() As Brush
        If ThemeManager.IsDark Then
            Return New SolidColorBrush(Color.FromRgb(52, 65, 86))
        End If
        Return Brushes.LightGray
    End Function

    Public Shared Function ToFlowDocument(markdown As String) As FlowDocument
        Dim document As New FlowDocument()
        If String.IsNullOrEmpty(markdown) Then Return document

        document.PageWidth = 999999
        document.PageHeight = 999999
        document.ColumnWidth = 999999

        Dim lines = Regex.Split(markdown, "\r\n|\r|\n")
        Dim i As Integer = 0
        While i < lines.Length
            Dim line = lines(i)

            ' Fenced code block
            If line.TrimStart().StartsWith("```") Then
                Dim info = line.TrimStart().Substring(3).Trim()
                Dim codeLines As New List(Of String)()
                i += 1
                While i < lines.Length AndAlso Not lines(i).TrimStart().StartsWith("```")
                    codeLines.Add(lines(i))
                    i += 1
                End While
                i += 1

                Dim para As New Paragraph()
                para.Margin = New Thickness(0, 6, 0, 6)
                Dim codeRun As New Run(String.Join(vbLf, codeLines))
                codeRun.FontFamily = New System.Windows.Media.FontFamily("Consolas")
                codeRun.FontSize = 12
                codeRun.Background = GetCodeBackground()
                codeRun.Foreground = New SolidColorBrush(GetTextColor())
                para.Inlines.Add(codeRun)
                document.Blocks.Add(para)
                Continue While
            End If

            ' Headers
            Dim headerMatch = Regex.Match(line, "^(#{1,6})\s+(.*)$")
            If headerMatch.Success Then
                Dim level = headerMatch.Groups(1).Value.Length
                Dim text = headerMatch.Groups(2).Value
                Dim para As New Paragraph()
                para.Margin = New Thickness(0, 8, 0, 4)
                para.FontSize = 16 - level
                para.FontWeight = System.Windows.FontWeights.Bold
                para.Foreground = New SolidColorBrush(GetTextColor())
                para.Inlines.AddRange(ParseInlines(text))
                document.Blocks.Add(para)
                i += 1
                Continue While
            End If

            ' Horizontal rule
            If Regex.IsMatch(line, "^[-*_]{3,}\s*$") Then
                Dim hr As New BlockUIContainer(New System.Windows.Shapes.Rectangle With {
                    .Height = 1,
                    .Fill = GetBorderColor(),
                    .HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
                })
                hr.Margin = New Thickness(0, 8, 0, 8)
                document.Blocks.Add(hr)
                i += 1
                Continue While
            End If

            ' Unordered list
            Dim listMatch = Regex.Match(line, "^[\s]*[-*+]\s+(.*)$")
            If listMatch.Success Then
                Dim list As New List()
                While i < lines.Length AndAlso Regex.IsMatch(lines(i), "^[\s]*[-*+]\s+")
                    Dim itemText = Regex.Replace(lines(i), "^[\s]*[-*+]\s+", "")
                    Dim item As New ListItem()
                    item.Blocks.Add(New Paragraph(ParseInlines(itemText)))
                    list.ListItems.Add(item)
                    i += 1
                End While
                document.Blocks.Add(list)
                Continue While
            End If

            ' Ordered list
            Dim orderedMatch = Regex.Match(line, "^[\s]*\d+\.\s+(.*)$")
            If orderedMatch.Success Then
                Dim list As New List()
                While i < lines.Length AndAlso Regex.IsMatch(lines(i), "^[\s]*\d+\.\s+")
                    Dim itemText = Regex.Replace(lines(i), "^[\s]*\d+\.\s+", "")
                    Dim item As New ListItem()
                    item.Blocks.Add(New Paragraph(ParseInlines(itemText)))
                    list.ListItems.Add(item)
                    i += 1
                End While
                document.Blocks.Add(list)
                Continue While
            End If

            ' Blockquote
            Dim quoteMatch = Regex.Match(line, "^>\s+(.*)$")
            If quoteMatch.Success Then
                Dim block As New Paragraph()
                block.Margin = New Thickness(0, 6, 0, 6)
                block.Padding = New Thickness(12, 0, 0, 0)
                block.BorderBrush = GetBorderColor()
                block.BorderThickness = New Thickness(2, 0, 0, 0)
                block.Foreground = New SolidColorBrush(GetMutedColor())
                block.Inlines.AddRange(ParseInlines(quoteMatch.Groups(1).Value))
                document.Blocks.Add(block)
                i += 1
                Continue While
            End If

            ' Regular paragraph (skip empty lines)
            If String.IsNullOrWhiteSpace(line) Then
                document.Blocks.Add(New Paragraph())
                i += 1
                Continue While
            End If

            Dim para2 As New Paragraph()
            para2.Margin = New Thickness(0, 4, 0, 4)
            para2.Foreground = New SolidColorBrush(GetTextColor())
            para2.Inlines.AddRange(ParseInlines(line))
            document.Blocks.Add(para2)
            i += 1
        End While

        Return document
    End Function

    Private Shared Function ParseInlines(text As String) As IEnumerable(Of Inline)
        Dim result As New List(Of Inline)()
        If String.IsNullOrEmpty(text) Then Return result

        ' Pattern for bold, italic, code, links, and plain text
        Dim pattern As String = "(\*\*.+?\*\*|\*.+?\*|`[^`]+`|\[[^\]]+\]\([^)]+\)|.)"
        Dim matches = Regex.Matches(text, pattern)

        For Each match As Match In matches
            Dim token = match.Value
            If token.StartsWith("**") AndAlso token.EndsWith("**") AndAlso token.Length >= 4 Then
                Dim bold As New Bold()
                bold.Inlines.AddRange(ParseInlines(token.Substring(2, token.Length - 4)))
                result.Add(bold)
            ElseIf token.StartsWith("*") AndAlso token.EndsWith("*") AndAlso token.Length >= 2 Then
                Dim italic As New Italic()
                italic.Inlines.AddRange(ParseInlines(token.Substring(1, token.Length - 2)))
                result.Add(italic)
            ElseIf token.StartsWith("`") AndAlso token.EndsWith("`") AndAlso token.Length >= 2 Then
                Dim codeRun As New Run(token.Substring(1, token.Length - 2))
                codeRun.FontFamily = New System.Windows.Media.FontFamily("Consolas")
                codeRun.Background = GetCodeBackground()
                codeRun.Foreground = New SolidColorBrush(GetTextColor())
                result.Add(codeRun)
            ElseIf token.StartsWith("[") AndAlso token.Contains("](") AndAlso token.EndsWith(")") Then
                Dim linkStart = token.IndexOf("[") + 1
                Dim linkEnd = token.IndexOf("](")
                Dim urlStart = linkEnd + 2
                Dim urlEnd = token.LastIndexOf(")")
                Dim linkText = token.Substring(linkStart, linkEnd - linkStart)
                Dim url = token.Substring(urlStart, urlEnd - urlStart)
                Dim hyperlink As New Hyperlink(New Run(linkText))
                hyperlink.NavigateUri = New Uri(url, UriKind.Absolute Or UriKind.Relative)
                AddHandler hyperlink.RequestNavigate, Sub(s, e)
                                                          Try
                                                              System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo With {
                                                                  .FileName = e.Uri.ToString(),
                                                                  .UseShellExecute = True
                                                              })
                                                          Catch
                                                          End Try
                                                          e.Handled = True
                                                      End Sub
                result.Add(hyperlink)
            Else
                result.Add(New Run(token) With {.Foreground = New SolidColorBrush(GetTextColor())})
            End If
        Next

        Return result
    End Function
End Class
