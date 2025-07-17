using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ToDoHicky
{
    // Main window class, implements INotifyPropertyChanged for data binding
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Collection of tasks, observable for UI updates
        private ObservableCollection<TaskItem> _tasks = new ObservableCollection<TaskItem>();
        // View source for filtering tasks (e.g., hide completed)
        private CollectionViewSource _filteredTasksView;
        // Tracks next available numeric task ID
        private int _nextTaskId = 1;
        // Flag to hide completed tasks
        private bool _hideCompleted;
        // Flag to enable/disable notes textbox based on selection
        private bool _hasSelectedTask;
        // Flag to track unsaved changes
        private bool _isDirty;

        // Property for tasks collection, notifies UI on changes
        public ObservableCollection<TaskItem> Tasks
        {
            get => _tasks;
            set
            {
                _tasks = value;
                _isDirty = true; // Mark as dirty when tasks collection changes
                OnPropertyChanged(nameof(Tasks));
                if (_filteredTasksView != null)
                {
                    _filteredTasksView.Source = value;
                    _filteredTasksView.View?.Refresh();
                }
            }
        }

        // Property for filtered tasks view, used in DataGrid
        public CollectionViewSource FilteredTasks
        {
            get => _filteredTasksView;
            set
            {
                _filteredTasksView = value;
                OnPropertyChanged(nameof(FilteredTasks));
            }
        }

        // Property to control hiding completed tasks
        public bool HideCompleted
        {
            get => _hideCompleted;
            set
            {
                _hideCompleted = value;
                OnPropertyChanged(nameof(HideCompleted));
                _filteredTasksView.View?.Refresh();
            }
        }

        // Property to track if a task is selected
        public bool HasSelectedTask
        {
            get => _hasSelectedTask;
            set
            {
                _hasSelectedTask = value;
                OnPropertyChanged(nameof(HasSelectedTask));
            }
        }

        // Property to track unsaved changes
        public bool IsDirty
        {
            get => _isDirty;
            set
            {
                _isDirty = value;
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        // Constructor: initializes UI and loads tasks
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            string csvDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CSV");
            if (!Directory.Exists(csvDirectory))
            {
                Directory.CreateDirectory(csvDirectory);
            }
            _filteredTasksView = new CollectionViewSource { Source = Tasks };
            _filteredTasksView.Filter += ApplyFilter;
            LoadTasksFromCsv();
            Closing += Window_Closing; // Subscribe to Closing event
        }

        // Handles window closing to prompt for saving unsaved changes
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show(
                    "Would you like to save before exiting?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        SaveTasksToCsv();
                        StatusText.Text = "Tasks saved successfully.";
                        IsDirty = false;
                    }
                    catch (IOException)
                    {
                        StatusText.Text = $"Error saving tasks.";
                        e.Cancel = true; // Cancel closing if save fails
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true; // Cancel closing
                }
                // MessageBoxResult.No allows closing without saving
            }
        }

        // Handles "Add Task" button click, creates new task with numeric TaskID
        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            Tasks.Add(new TaskItem(this)
            {
                TaskID = _nextTaskId.ToString(),
                TaskName = "New Task",
                Status = "Not Started",
                AssignedUser = Environment.UserName,
                DueDate = null,
                Notes = "",
                Priority = "Medium"
            });
            _nextTaskId++;
            _isDirty = true; // Mark as dirty
            _filteredTasksView.View?.Refresh();
        }

        // Handles "Save" button click, writes tasks to CSV
        private void SaveTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveTasksToCsv();
                StatusText.Text = "Tasks saved successfully.";
                IsDirty = false; // Clear dirty flag after successful save
            }
            catch (IOException)
            {
                StatusText.Text = $"Error saving tasks.";
            }
        }

        // Handles key presses in DataGrid (e.g., Delete key to remove task)
        private void TasksGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && TasksGrid.SelectedItem is TaskItem selectedTask && TasksGrid.CurrentColumn != NotesColumn)
            {
                var result = MessageBox.Show($"Are you sure you want to delete the task '{selectedTask.TaskName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Tasks.Remove(selectedTask);
                    StatusText.Text = "Task deleted successfully.";
                    _isDirty = true; // Mark as dirty
                    UpdateNextTaskId();
                    _filteredTasksView.View?.Refresh();
                }
                e.Handled = true;
            }
        }

        // Handles mouse clicks in DataGrid to clear selection
        private void TasksGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(TasksGrid, e.GetPosition(TasksGrid));
            if (hit != null && !(hit.VisualHit is DataGridRow))
            {
                TasksGrid.SelectedItem = null;
                HasSelectedTask = false;
                StatusText.Text = "Selection cleared.";
            }
        }

        // Updates HasSelectedTask when selection changes
        private void TasksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HasSelectedTask = TasksGrid.SelectedItem != null;
        }

        // Handles checkbox checked state to hide completed tasks
        private void HideCompletedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            HideCompleted = true;
        }

        // Handles checkbox unchecked state to show all tasks
        private void HideCompletedCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            HideCompleted = false;
        }

        // Filters tasks based on HideCompleted setting
        private void ApplyFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is TaskItem task)
            {
                e.Accepted = !HideCompleted || task.Status != "Completed";
            }
            else
            {
                e.Accepted = false;
            }
        }

        // Loads tasks from the most recent CSV file
        private void LoadTasksFromCsv()
        {
            // Find the most recent CSV file
            string csvFilePath = GetMostRecentCsvFile();
            if (csvFilePath == null)
            {
                StatusText.Text = "No CSV files found in the CSV subdirectory.";
                return;
            }

            try
            {
                // Read entire file as a single string to handle multi-line fields
                string csvContent = File.ReadAllText(csvFilePath, Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(csvContent))
                {
                    StatusText.Text = $"CSV file at {csvFilePath} is empty.";
                    return;
                }

                var lines = ParseCsvLines(csvContent);
                if (lines.Length == 0)
                {
                    StatusText.Text = $"CSV file at {csvFilePath} contains no valid lines.";
                    return;
                }

                var headers = ParseCsvLine(lines[0]).Select(h => h.Trim('"').Trim()).ToArray();
                int idIndex = Array.IndexOf(headers, "TaskID");
                int taskIndex = Array.IndexOf(headers, "Task");
                int statusIndex = Array.IndexOf(headers, "Status");
                int userIndex = Array.IndexOf(headers, "AssignedUser");
                int dueDateIndex = Array.IndexOf(headers, "DueDate");
                int priorityIndex = Array.IndexOf(headers, "Priority");
                int notesIndex = Array.IndexOf(headers, "Notes");

                // Validate all required headers are present
                if (idIndex < 0 || taskIndex < 0 || statusIndex < 0 || userIndex < 0 ||
                    dueDateIndex < 0 || priorityIndex < 0 || notesIndex < 0)
                {
                    StatusText.Text = $"Missing required headers in CSV at {csvFilePath}. Expected: TaskID,Task,Status,AssignedUser,DueDate,Priority,Notes. Found: {string.Join(",", headers)}";
                    return;
                }

                Tasks.Clear();
                for (int i = 1; i < lines.Length; i++)
                {
                    var fields = ParseCsvLine(lines[i]);
                    if (fields.Length < 7) // Ensure at least 7 fields (required columns)
                    {
                        StatusText.Text = $"Row {i + 1} has too few fields in {csvFilePath}: {string.Join(",", fields)}";
                        continue;
                    }

                    string taskId = fields[idIndex].Trim('"');
                    if (string.IsNullOrEmpty(taskId))
                    {
                        StatusText.Text = $"Invalid TaskID in row {i + 1} of {csvFilePath}: {fields[idIndex]}";
                        continue;
                    }

                    DateTime? dueDate = null;
                    string dueDateStr = fields[dueDateIndex].Trim('"');
                    if (!string.IsNullOrEmpty(dueDateStr))
                    {
                        if (DateTime.TryParseExact(dueDateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                        {
                            dueDate = parsedDate;
                        }
                        else
                        {
                            StatusText.Text = $"Invalid DueDate format in row {i + 1} of {csvFilePath}: {dueDateStr}";
                            continue;
                        }
                    }

                    string status = fields[statusIndex].Trim('"');
                    if (!new[] { "Not Started", "In Progress", "Completed" }.Contains(status))
                    {
                        status = "Not Started";
                        StatusText.Text = $"Invalid Status in row {i + 1} of {csvFilePath}: {fields[statusIndex]}, defaulting to Not Started";
                    }

                    string priority = fields[priorityIndex].Trim('"');
                    if (!new[] { "Low", "Medium", "High", "URGENT" }.Contains(priority))
                    {
                        priority = "Medium";
                        StatusText.Text = $"Invalid Priority in row {i + 1} of {csvFilePath}: {fields[priorityIndex]}, defaulting to Medium";
                    }

                    string notes = fields[notesIndex].Trim('"');

                    Tasks.Add(new TaskItem(this)
                    {
                        TaskID = taskId,
                        TaskName = fields[taskIndex].Trim('"'),
                        Status = status,
                        AssignedUser = fields[userIndex].Trim('"'),
                        DueDate = dueDate,
                        Notes = notes,
                        Priority = priority
                    });
                }

                UpdateNextTaskId();
                IsDirty = false; // Clear dirty flag after loading

                // Apply default sort by TaskID
                _filteredTasksView.SortDescriptions.Clear();
                _filteredTasksView.SortDescriptions.Add(new SortDescription(nameof(TaskItem.TaskID), ListSortDirection.Ascending));

                if (Tasks.Count == 0 && lines.Length > 1)
                {
                    StatusText.Text = $"No valid tasks loaded from {csvFilePath}. Check CSV format.";
                }
                else
                {
                    StatusText.Text = $"Loaded {Tasks.Count} tasks successfully from {csvFilePath}.";
                }
            }
            catch (IOException ex)
            {
                StatusText.Text = $"Error loading tasks from {csvFilePath}: {ex.Message}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Unexpected error loading tasks from {csvFilePath}: {ex.Message}";
            }
        }

        // Updates _nextTaskId based on the maximum numeric TaskID in the Tasks collection
        public void UpdateNextTaskId()
        {
            int maxNumericId = 0;
            foreach (var task in Tasks)
            {
                if (int.TryParse(task.TaskID, out int numericId))
                {
                    maxNumericId = Math.Max(maxNumericId, numericId);
                }
            }
            _nextTaskId = maxNumericId + 1;
        }

        // Checks if a TaskID is unique among all tasks
        public bool IsTaskIdUnique(string taskId, TaskItem currentTask)
        {
            return !Tasks.Any(t => t != currentTask && t.TaskID == taskId);
        }

        // Saves tasks to a new CSV file with timestamp in the filename
        private void SaveTasksToCsv()
        {
            try
            {
                // Generate filename with timestamp (e.g., tasks_2025-07-15_10-27-23.csv)
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string csvDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CSV");
                string csvFilePath = Path.Combine(csvDirectory, $"tasks_{timestamp}.csv");
                var lines = new[] { "\"TaskID\",\"Task\",\"Status\",\"AssignedUser\",\"DueDate\",\"Priority\",\"Notes\"" }.Concat(
                    Tasks.Select(t => $"\"{EscapeCsvField(t.TaskID)}\",\"{EscapeCsvField(t.TaskName)}\",\"{EscapeCsvField(t.Status)}\",\"{EscapeCsvField(t.AssignedUser)}\",\"{EscapeCsvField(t.DueDate?.ToString("yyyy-MM-dd") ?? "")}\",\"{EscapeCsvField(t.Priority)}\",\"{EscapeCsvField(t.Notes)}\"")
                );
                File.WriteAllLines(csvFilePath, lines, Encoding.UTF8);
            }
            catch (IOException)
            {
                throw; // Rethrow to handle in caller
            }
        }

        // Finds the most recent CSV file in the application directory
        private string GetMostRecentCsvFile()
        {
            try
            {
                var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CSV");
                var csvFiles = Directory.GetFiles(directory, "tasks_*.csv")
                    .Where(f => {
                        string fileName = Path.GetFileNameWithoutExtension(f);
                        if (!fileName.StartsWith("tasks_")) return false;
                        string timestamp = fileName.Substring(6); // Get part after "tasks_"
                        return DateTime.TryParseExact(timestamp, "yyyy-MM-dd_HH-mm-ss",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out _);
                    })
                    .ToList();

                if (!csvFiles.Any())
                {
                    return null; // No valid CSV files found
                }

                // Sort files by timestamp in descending order and take the most recent
                var mostRecentFile = csvFiles
                    .Select(f => new { Path = f, FileName = Path.GetFileNameWithoutExtension(f) })
                    .OrderByDescending(f => DateTime.ParseExact(f.FileName.Substring(6), "yyyy-MM-dd_HH-mm-ss",
                        System.Globalization.CultureInfo.InvariantCulture))
                    .FirstOrDefault();

                return mostRecentFile?.Path;
            }
            catch (Exception)
            {
                return null; // Return null if there's an error accessing files
            }
        }

        // Escapes double quotes in CSV fields to prevent format issues
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            return field.Replace("\"", "''");
        }

        // Splits CSV content into logical rows, respecting quoted fields with newlines
        private string[] ParseCsvLines(string csvContent)
        {
            var lines = new List<string>();
            var currentLine = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csvContent.Length; i++)
            {
                char c = csvContent[i];

                if (c == '"' && (i == 0 || csvContent[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    currentLine.Append(c);
                    continue;
                }

                if (c == '\n' && !inQuotes)
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString().Trim());
                        currentLine.Clear();
                    }
                    continue;
                }

                currentLine.Append(c);
            }

            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString().Trim());
            }

            return lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        }

        // Parses a CSV line into fields, handling quoted strings and newlines
        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                // Handle double quotes to start/end quoted fields
                if (c == '"' && (i == 0 || (i > 0 && line[i - 1] != '\\')))
                {
                    inQuotes = !inQuotes;
                    field.Append(c);
                    continue;
                }

                // Handle commas outside quotes to separate fields
                if (c == ',' && !inQuotes)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    continue;
                }

                // Append character to current field
                field.Append(c);
            }

            // Add the last field if it exists
            if (field.Length > 0)
            {
                fields.Add(field.ToString());
            }

            return fields.ToArray();
        }

        // Property change event for data binding
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Data model for a single task, supports property change notifications
    public class TaskItem : INotifyPropertyChanged
    {
        private readonly MainWindow _mainWindow;
        private string _taskID;
        private string _taskName;
        private string _status;
        private string _assignedUser;
        private DateTime? _dueDate;
        private string _notes;
        private string _priority;

        // Constructor to pass MainWindow reference for TaskID validation
        public TaskItem(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        // Constructor for backward compatibility
        public TaskItem() : this(null) { }

        // Unique ID for the task, now a string
        public string TaskID
        {
            get => _taskID;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _mainWindow?.StatusText.SetCurrentValue(TextBlock.TextProperty, "TaskID cannot be empty.");
                    return;
                }
                if (_mainWindow != null && !_mainWindow.IsTaskIdUnique(value, this))
                {
                    _mainWindow?.StatusText.SetCurrentValue(TextBlock.TextProperty, $"TaskID '{value}' is already in use.");
                    return;
                }
                _taskID = value;
                _mainWindow.IsDirty = true; // Mark as dirty
                OnPropertyChanged(nameof(TaskID));
                _mainWindow?.UpdateNextTaskId();
            }
        }

        // Name or description of the task
        public string TaskName
        {
            get => _taskName;
            set
            {
                _taskName = value;
                _mainWindow.IsDirty = true; // Mark as dirty
                OnPropertyChanged(nameof(TaskName));
            }
        }

        // Task status (Not Started, In Progress, Completed)
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                _mainWindow.IsDirty = true; // Mark as dirty
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(HeatMapColor));
            }
        }

        // User assigned to the task
        public string AssignedUser
        {
            get => _assignedUser; // Fixed bug: was incorrectly returning _taskID
            set
            {
                _assignedUser = value;
                _mainWindow.IsDirty = true; // Mark as dirty
                OnPropertyChanged(nameof(AssignedUser));
            }
        }

        // Optional due date for the task
        public DateTime? DueDate
        {
            get => _dueDate;
            set
            {
                _dueDate = value;
                _mainWindow.IsDirty = true; // Mark as dirty
                OnPropertyChanged(nameof(DueDate));
                OnPropertyChanged(nameof(HeatMapColor));
            }
        }

        // Additional notes for the task
        public string Notes
        {
            get => _notes;
            set
            {
                _notes = value;
                _mainWindow.IsDirty = true; // Mark as dirty
                OnPropertyChanged(nameof(Notes));
            }
        }

        // Priority level (Low, Medium, High, URGENT)
        public string Priority
        {
            get => _priority;
            set
            {
                _priority = value;
                _mainWindow.IsDirty = true; // Mark as dirty
                OnPropertyChanged(nameof(Priority));
                OnPropertyChanged(nameof(HeatMapColor));
            }
        }

        // Determines row background color based on task status and conditions
        public SolidColorBrush HeatMapColor
        {
            get
            {
                // Completed tasks are light blue (SkyBlue)
                if (Status == "Completed")
                {
                    return new SolidColorBrush(Color.FromRgb(135, 206, 235));
                }

                // Check due date conditions for non-completed tasks
                if (DueDate.HasValue)
                {
                    var today = DateTime.Today;
                    // Past due tasks are red
                    if (DueDate.Value.Date < today)
                    {
                        return Brushes.Red;
                    }
                    // Due today tasks are yellow
                    if (DueDate.Value.Date == today)
                    {
                        return Brushes.Yellow;
                    }
                }

                // Urgent tasks are orange
                if (Priority == "URGENT")
                {
                    return Brushes.Orange;
                }

                // In Progress tasks are light green
                if (Status == "In Progress")
                {
                    return new SolidColorBrush(Color.FromRgb(144, 238, 144));
                }

                // Not Started tasks are grey
                return Brushes.LightGray;
            }
        }

        // Property change event for data binding
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Converter to flatten newlines in Notes for DataGrid display
    public class FlattenNewlinesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.ToString().Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Validation rule for non-empty strings
    public class NonEmptyStringValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            string input = value as string;
            if (string.IsNullOrWhiteSpace(input))
            {
                return new ValidationResult(false, "TaskID cannot be empty.");
            }
            return ValidationResult.ValidResult;
        }
    }
}