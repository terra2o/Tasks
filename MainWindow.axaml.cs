using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    private void PopulateTasks(TaskData[] tasks)
    {
        Lists.Clear();

        foreach (var t in tasks)
        {
            var name = (t.Name ?? "").Trim();
            if (string.IsNullOrEmpty(name)) continue;

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
        if (string.IsNullOrEmpty(name)) return;

        if (name.Length > 30) name = name.Substring(0, 30);

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

        // Fallback: match by name if Content is the name.
        if (sender is Button fallbackBtn && fallbackBtn.Content is string name)
        {
            var match = Lists.FirstOrDefault(t => t.Name == name);
            if (match != null) SelectedTask = match;
        }
    }

    private void RemoveTask_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is TaskItem task)
        {
            RemoveTask(task);
        }
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
        try { SaveTasks(GetDataFilePath()); } catch (Exception ex) { Console.WriteLine($"Save error: {ex}"); }
    }

    private void TaskItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendSave) return;
        if (e.PropertyName == nameof(TaskItem.IsChecked) || e.PropertyName == nameof(TaskItem.Content))
        {
            try { SaveTasks(GetDataFilePath()); } catch (Exception ex) { Console.WriteLine($"Save error: {ex}"); }
        }
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
