Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.Threading.Tasks
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Documents
Imports System.Windows.Input

Partial Public Class TagAI
    Private Shared ReadOnly DesktopGuide As String =
        "Overview: Review sales, revenue, inventory, and recent shop activity. Choose a reporting period to change the overview." & vbLf &
        "Products & pricing: Browse products, stock, categories, and prices. Add, view, edit, or delete products and update inventory." & vbLf &
        "Sales: Review orders, payment state, totals, and customer details." & vbLf &
        "Customer debts: Record customer balances, review payment history, and record payments. Debt records are stored locally on this computer." & vbLf &
        "Product reviews: Read customer ratings and comments for products." & vbLf &
        "Insights: Explore sales and performance trends over selectable reporting periods." & vbLf &
        "Flash Sales: Create and review time-limited product offers." & vbLf &
        "Coupons: Create and review discount codes and their usage." & vbLf &
        "Reels / Status: Manage short product videos and store status content." & vbLf &
        "Store settings: Edit store profile details and storefront appearance." & vbLf &
        "Storefront Editor: Choose a storefront theme and layout, preview the storefront, and publish its settings." & vbLf &
        "Stocktakes: Record physical stock counts and compare counted quantities with expected inventory. Stocktakes are stored locally on this computer." & vbLf &
        "Appearance: Use the title bar theme button to switch between light and dark mode. The sidebar can be collapsed, and its search filters navigation pages." & vbLf &
        "TagAI: Ask for help with pages and features. TagAI is read-only."

    Private currentPageName As String
    Private isBusy As Boolean

    Public Sub New(Optional currentPage As String = "Overview")
        InitializeComponent()
        currentPageName = If(String.IsNullOrWhiteSpace(currentPage), "Overview", currentPage.Trim())
        TxtCurrentPage.Text = currentPageName
        AddTurn("assistant",
                "I can explain Tagit pages and features. Select a page from the sidebar first, then ask a question.")
    End Sub

    Private Sub AddTurn(role As String, content As String)
        Dim roleBlock As New Paragraph()
        roleBlock.Margin = New Thickness(0, 0, 0, 10)

        Dim roleRun As New Run(If(role = "assistant", "TagAI", "You") & ": ")
        roleRun.FontWeight = FontWeights.SemiBold
        If ThemeManager.IsDark Then
            roleRun.Foreground = If(role = "assistant", New System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 58, 112)), New System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 244, 246)))
        Else
            roleRun.Foreground = If(role = "assistant", New System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 58, 112)), New System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32)))
        End If
        roleBlock.Inlines.Add(roleRun)

        ConversationRichText.Document.Blocks.Add(roleBlock)

        Try
            Dim doc = MarkdownConverter.ToFlowDocument(content)
            Dim blocks = doc.Blocks.ToList()
            For Each block In blocks
                ConversationRichText.Document.Blocks.Add(block)
            Next
        Catch ex As Exception
            Dim fallbackBlock As New Paragraph()
            fallbackBlock.Inlines.Add(New Run(content))
            ConversationRichText.Document.Blocks.Add(fallbackBlock)
        End Try

        ConversationRichText.ScrollToEnd()
    End Sub

    Private Async Sub BtnAsk_Click(sender As Object, e As RoutedEventArgs)
        Await SubmitQuestionAsync(TxtQuestion.Text)
    End Sub

    Private Async Sub TxtQuestion_KeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Enter AndAlso Not Keyboard.IsKeyDown(Key.LeftShift) AndAlso Not Keyboard.IsKeyDown(Key.RightShift) Then
            e.Handled = True
            Await SubmitQuestionAsync(TxtQuestion.Text)
        End If
    End Sub

    Private Async Sub BtnExplainCurrentPage_Click(sender As Object, e As RoutedEventArgs)
        Await SubmitQuestionAsync("Explain the " & currentPageName & " page and what I can do there.", True)
    End Sub

    Private Async Sub BtnExplainFeatures_Click(sender As Object, e As RoutedEventArgs)
        Await SubmitQuestionAsync("Give me a guide to the main pages and features in Tagit Shop.", True)
    End Sub

    Private Async Function SubmitQuestionAsync(question As String, Optional helpOnly As Boolean = False) As Task
        Dim cleanQuestion = If(question, String.Empty).Trim()
        If cleanQuestion.Length = 0 OrElse isBusy Then Return

        AddTurn("user", cleanQuestion)
        TxtQuestion.Clear()
        SetBusy(True)
        Try
            Dim reply As String
            If AppSession.Mode = StoreMode.Demo Then
                reply = DemoAnswer(cleanQuestion)
            Else
                Dim safeHistory = New List(Of String)()
                reply = Await SupabaseService.AskDesktopAssistantAsync(
                    cleanQuestion,
                    currentPageName,
                    DesktopGuide,
                    "[]",
                    safeHistory
                )
            End If
            AddTurn("assistant", reply)
        Catch ex As Exception
            AddTurn("assistant", "TagAI could not complete that request: " & ex.Message)
        Finally
            SetBusy(False)
            TxtQuestion.Focus()
        End Try
    End Function

    Private Function DemoAnswer(question As String) As String
        Dim normalized = question.ToLowerInvariant()
        If normalized.Contains("this page") OrElse normalized.Contains("current page") OrElse
            normalized.Contains(currentPageName.ToLowerInvariant()) Then
            Dim currentGuide = GuideLineFor(currentPageName)
            If Not String.IsNullOrWhiteSpace(currentGuide) Then
                Return currentGuide & " You are currently on the " & currentPageName & " page. Demo mode provides this local guide; generative answers require a live store sign-in."
            End If
        End If

        Dim matchedGuides = DesktopGuide.Split({vbLf}, StringSplitOptions.RemoveEmptyEntries).
            Where(Function(line) normalized.Split({" "c, "?"c, "."c, ","c}, StringSplitOptions.RemoveEmptyEntries).
                Any(Function(word) word.Length >= 4 AndAlso line.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)).
            Take(3).
            ToList()
        If matchedGuides.Count > 0 Then
            Return String.Join(vbLf, matchedGuides) & vbLf &
                "This local guide is available in demo mode. Sign in to a live store for generative TagAI answers."
        End If

        Return "I can explain each Tagit page and feature. Select a page from the sidebar first, then ask a question. Demo mode uses a local guide; generative answers require a live store sign-in."
    End Function

    Private Function GuideLineFor(pageName As String) As String
        Return DesktopGuide.Split({vbLf}, StringSplitOptions.RemoveEmptyEntries).
            FirstOrDefault(Function(line) line.StartsWith(pageName & ":", StringComparison.OrdinalIgnoreCase))
    End Function

    Private Sub SetBusy(busy As Boolean)
        isBusy = busy
        BtnAsk.IsEnabled = Not busy
        TxtQuestion.IsEnabled = Not busy
        BtnAsk.Content = If(busy, "Working...", "Ask TagAI")
    End Sub
End Class
