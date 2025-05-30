using CodeAIExtension.Models;
using CodeAIExtension.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CodeAIExtension.ViewModels;

public class ChatToolWindowViewModel : INotifyPropertyChanged
{
    private readonly IChatService _chatService;
    private readonly CancellationTokenSource _cts = new();

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = new ObservableCollection<ChatMessageViewModel>();

    private string _inputText;
    public string InputText
    {
        get => _inputText;
        set
        {
            _inputText = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)SendCommand).RaiseCanExecuteChanged();
        }
    }

    private bool _isStreaming = false;
    public bool IsStreaming
    {
        get => _isStreaming;
        set
        {
            _isStreaming = value;
            OnPropertyChanged();
            ((AsyncRelayCommand)SendCommand).RaiseCanExecuteChanged();
        }
    }

    public ICommand SendCommand { get; }

    public ChatToolWindowViewModel(IChatService chatService)
    {
        _chatService = chatService;
        SendCommand = new AsyncRelayCommand(SendAsync, () => !IsStreaming && !string.IsNullOrWhiteSpace(InputText));
    }

    private async Task SendAsync()
    {
        var userMsg = new ChatMessage(Role.User, InputText.Trim());
        InputText = string.Empty;
        var userVm = new ChatMessageViewModel(userMsg);
        Messages.Add(userVm);

        var assistantBuffer = new StringBuilder();
        var assistantModel = new ChatMessage(Role.Assistant, string.Empty);
        var assistantVm = new ChatMessageViewModel(assistantModel);
        Messages.Add(assistantVm);

        IsStreaming = true;

        var progress = new Progress<string>(delta =>
        {
            assistantBuffer.Append(delta);
            assistantModel.Content = assistantBuffer.ToString();

            assistantVm.Segments.Clear();
            foreach (var seg in ChatMessageViewModel.ParseMarkdown(assistantModel.Content))
                assistantVm.Segments.Add(seg);
        });

        try
        {
            var request = new ChatRequest
            {
                Prompt = userMsg.Content,
                Context = string.Empty,
                Language = "csharp",
                Temperature = 0.7,
                MaxTokens = 1000
            };

            await _chatService.StreamChatAsync(request, progress, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessageViewModel(new ChatMessage(Role.System, "Ошибка: " + ex.Message)));
        }
        finally
        {
            IsStreaming = false;
            InputText = string.Empty;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
