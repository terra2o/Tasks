using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Reactive;
using Avalonia.Threading;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tasks;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // ObservableCollection must only be modified on the UI thread.
    // Using this instead of a List because List doesn't notify UI.
    public ObservableCollection<TaskItem> Lists { get; } = new();

    private TaskItem? _selectedTask;
    public TaskItem? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (_selectedTask == value) return;
            _selectedTask = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTask)));
        }
    }

    private TextBox? _nameBox;
    private Button? _flyoutButton;

    private bool _suspendSave = false;
    private string ?_renameBackup;

    DispatcherTimer? _saveTimer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _nameBox = this.FindControl<TextBox>("NameBox");
        _flyoutButton = this.FindControl<Button>("FlyoutButton");

        // Listens for add/remove so we can autosave the collection change
        Lists.CollectionChanged += Lists_CollectionChanged;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        _suspendSave = true;

        try
        {
            var filePath = GetDataFilePath();

            var tasks = await Task.Run(() =>
            {
                if (!File.Exists(filePath))
                    return Array.Empty<TaskData>();

                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<TaskData[]>(json)
                    ?? Array.Empty<TaskData>();
            });

            PopulateTasks(tasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load tasks: {ex}");
        }
        finally
        {
            _suspendSave = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Lists.CollectionChanged -= Lists_CollectionChanged;
        Opened -= OnOpened;

        foreach (var t in Lists)
            t.PropertyChanged -= TaskItem_PropertyChanged;

        base.OnClosed(e);
    }

    private void PopulateTasks(TaskData[] tasks)
    {
        Lists.Clear();

        foreach (var t in tasks)
        {
            var name = (t.Name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var item = new TaskItem
            {
                Name = name,
                Content = t.Content ?? "",
                IsChecked = t.IsChecked
            };

            AddTask(item);
        }

        SelectedTask = Lists.FirstOrDefault();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        MinWidth = Width;
        MinHeight = Height;
        SizeToContent = SizeToContent.Manual;
    }

    private void ConfirmAdd(object? sender, RoutedEventArgs? e)
    {
        if (_nameBox is null) return;

        var nameRaw = _nameBox.Text ?? "";
        var name = nameRaw.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        // Checking if ANY existing task already has the same name.
        // Detect a duplicate, abort the add, clear input only for UX.
        if (Lists.Any(t => string.Equals(t.Name, name, StringComparison.Ordinal)))
        {
            _nameBox.Text = string.Empty;
            return;
        }

        var newItem = new TaskItem { Name = name, Content = string.Empty, IsChecked = false };
        AddTask(newItem);

        // Show it and clear input.
        SelectedTask = newItem;
        _nameBox.Text = string.Empty;
        _flyoutButton?.Flyout?.Hide();
    }

    private void AddTask(TaskItem item)
    {
        Lists.Add(item);
        item.PropertyChanged += TaskItem_PropertyChanged; // Guarantees every task gets its PropertyChanged hooked.
    }

    private void SwitchTask(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is TaskItem clickedTask)
        {
            SelectedTask = clickedTask;
            return;
        }
    }

    private void RemoveTask_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is TaskItem task)
        {
            RemoveTask(task);
        }
    }

    private void RenameTask_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi &&
            mi.DataContext is TaskItem task)
        {
            foreach (var t in Lists)
                t.IsRenaming = false;

            _renameBackup = task.Name;
            task.IsRenaming = true;
        }
    }

    private void CommitRename(TaskItem task)
    {
        if (string.IsNullOrWhiteSpace(task.Name))
        {
            task.Name = _renameBackup!;
        }
        if (Lists.Any(t => t != task &&
            string.Equals(t.Name, task.Name, StringComparison.Ordinal)))
        {
            task.Name = _renameBackup!;
        }


        task.IsRenaming = false;
    }

    private void Rename_KeyUp(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox tb &&
            tb.DataContext is TaskItem task &&
            e.Key == Key.Enter)
        {
            CommitRename(task);
            e.Handled = true;
        }
    }

    private void Rename_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb &&
            tb.DataContext is TaskItem task)
        {
            CommitRename(task);
        }
    }

    private void RenameBox_AttachedToVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox tb)
            return;

        tb.PropertyChanged += (_, args) =>
        {
            if (args.Property == IsVisibleProperty &&
                tb.IsVisible)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    tb.Focus();
                    tb.SelectAll();
                }, DispatcherPriority.Background);
            }
        };
    }

    private void RemoveTask(TaskItem task)
    {
        task.PropertyChanged -= TaskItem_PropertyChanged;
        if (SelectedTask == task) SelectedTask = null;
        Lists.Remove(task);
        // Save triggered by Lists_CollectionChanged (unless suspended)
    }

    private void Lists_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_suspendSave) return;
        try { RequestSave(); } catch (Exception ex) { Console.WriteLine($"Save error: {ex}"); }
    }

    private void TaskItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendSave) return;

        if (e.PropertyName == nameof(TaskItem.IsRenaming))
            return;

        RequestSave();
    }

    private void NameBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConfirmAdd(null, null);
            e.Handled = true;
        }
    }

    public class TaskData
    {
        public string Name { get; set; } = "";
        public string Content { get; set; } = "";
        public bool IsChecked { get; set; } = false;
    }

    private void SaveTasks(string filePath)
    {
        try
        {
            var tasks = Lists.Select(li => new TaskData
            {
                Name = li.Name,
                IsChecked = li.IsChecked,
                Content = li.Content ?? ""
            }).ToArray();

            var json = JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SaveTasks failed: {ex}");
        }
    }

    private void RequestSave()
    {
        _saveTimer ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };

        _saveTimer.Tick -= SaveTick;
        _saveTimer.Tick += SaveTick;
        _saveTimer.Start();
    }

    private void SaveTick(object? sender, EventArgs e)
    {
        _saveTimer!.Stop();
        SaveTasks(GetDataFilePath());
    }

    private static string GetDataFilePath()
    {
        string folder;
        if (OperatingSystem.IsWindows())
            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tasks_terra2o");
        else if (OperatingSystem.IsMacOS())
            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tasks_terra2o");
        else
            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "Tasks_terra2o");

        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "tasks.json");
    }

    public new event PropertyChangedEventHandler? PropertyChanged;
}

public class TaskItem : INotifyPropertyChanged
{
    private string _name = "";
    private bool _isRenaming;
    public string Name
    
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            if (_isRenaming == value) return;
            _isRenaming = value;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(IsRenaming)));
        }
    }

    private string _content = "";
    public string Content
    {
        get => _content;
        set
        {
            if (_content == value) return;
            _content = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
        }
    }

    private bool _isChecked = false;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
