// Main code-behind for TO-DO-hicky app, handling task management and UI logic
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
        // Path to the CSV file for task persistence
        private readonly string _csvFilePath = "tasks.csv";
        // Tracks next available task ID
        private int _nextTaskId = 1;
        // Flag to hide completed tasks
        private bool _hideCompleted;
        // Flag to enable/disable notes textbox based on selection
        private bool _hasSelectedTask;

        // Property for tasks collection, notifies UI on changes
        public ObservableCollection<TaskItem> Tasks
        {
            get => _tasks;
            set
            {
                _tasks = value;
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

        // Constructor: initializes UI and loads tasks
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _filteredTasksView = new CollectionViewSource { Source = Tasks };
            _filteredTasksView.Filter += ApplyFilter;
            LoadTasksFromCsv();
        }

        // Handles "Add Task" button click, creates new task
        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            Tasks.Add(new TaskItem
            {
                TaskID = _nextTaskId++,
                TaskName = "New Task",
                Status = "Not Started",
                AssignedUser = Environment.UserName,
                DueDate = null,
                Notes = "",
                Priority = "Medium"
            });
            _filteredTasksView.View?.Refresh();
        }

        // Handles "Save" button click, writes tasks to CSV
        private void SaveTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveTasksToCsv();
                StatusText.Text = "Tasks saved successfully.";
            }
            catch (IOException ex)
            {
                StatusText.Text = $"Error saving tasks: {ex.Message}";
            }
        }

        // Handles key presses in DataGrid (e.g., Delete key to remove task)
        private void TasksGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && TasksGrid.SelectedItem is TaskItem selectedTask)
            {
                var result = MessageBox.Show($"Are you sure you want to delete the task '{selectedTask.TaskName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Tasks.Remove(selectedTask);
                    StatusText.Text = "Task deleted successfully.";
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

        // Loads tasks from CSV file
        private void LoadTasksFromCsv()
        {
            if (!File.Exists(_csvFilePath))
            {
                StatusText.Text = $"CSV file not found at {_csvFilePath}";
                return;
            }

            try
            {
                // Read entire file as a single string to handle multi-line fields
                string csvContent = File.ReadAllText(_csvFilePath, Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(csvContent))
                {
                    StatusText.Text = "CSV file is empty";
                    return;
                }

                var lines = ParseCsvLines(csvContent);
                if (lines.Length == 0)
                {
                    StatusText.Text = "CSV file contains no valid lines";
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
                    StatusText.Text = "Missing required headers in CSV. Expected: TaskID,Task,Status,AssignedUser,DueDate,Priority,Notes. Found: " + string.Join(",", headers);
                    return;
                }

                Tasks.Clear();
                for (int i = 1; i < lines.Length; i++)
                {
                    var fields = ParseCsvLine(lines[i]);
                    if (fields.Length < 7) // Ensure at least 7 fields (required columns)
                    {
                        StatusText.Text = $"Row {i + 1} has too few fields: {string.Join(",", fields)}";
                        continue;
                    }

                    if (int.TryParse(fields[idIndex].Trim('"'), out int taskId))
                    {
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
                                StatusText.Text = $"Invalid DueDate format in row {i + 1}: {dueDateStr}";
                                continue;
                            }
                        }

                        string status = fields[statusIndex].Trim('"');
                        if (!new[] { "Not Started", "In Progress", "Completed" }.Contains(status))
                        {
                            status = "Not Started";
                            StatusText.Text = $"Invalid Status in row {i + 1}: {fields[statusIndex]}, defaulting to Not Started";
                        }

                        string priority = fields[priorityIndex].Trim('"');
                        if (!new[] { "Low", "Medium", "High", "URGENT" }.Contains(priority))
                        {
                            priority = "Medium";
                            StatusText.Text = $"Invalid Priority in row {i + 1}: {fields[priorityIndex]}, defaulting to Medium";
                        }

                        string notes = fields[notesIndex].Trim('"');

                        Tasks.Add(new TaskItem
                        {
                            TaskID = taskId,
                            TaskName = fields[taskIndex].Trim('"'),
                            Status = status,
                            AssignedUser = fields[userIndex].Trim('"'),
                            DueDate = dueDate,
                            Notes = notes,
                            Priority = priority
                        });
                        _nextTaskId = Math.Max(_nextTaskId, taskId + 1);
                    }
                    else
                    {
                        StatusText.Text = $"Invalid TaskID in row {i + 1}: {fields[idIndex]}";
                    }
                }

                if (Tasks.Count == 0 && lines.Length > 1)
                {
                    StatusText.Text = "No valid tasks loaded. Check CSV format.";
                }
                else
                {
                    StatusText.Text = $"Loaded {Tasks.Count} tasks successfully.";
                }
            }
            catch (IOException ex)
            {
                StatusText.Text = $"Error loading tasks: {ex.Message}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Unexpected error loading tasks: {ex.Message}";
            }
        }

        // Saves tasks to CSV file
        private void SaveTasksToCsv()
        {
            try
            {
                var lines = new[] { "\"TaskID\",\"Task\",\"Status\",\"AssignedUser\",\"DueDate\",\"Priority\",\"Notes\"" }.Concat(
                    Tasks.Select(t => $"\"{t.TaskID}\",\"{EscapeCsvField(t.TaskName)}\",\"{EscapeCsvField(t.Status)}\",\"{EscapeCsvField(t.AssignedUser)}\",\"{EscapeCsvField(t.DueDate?.ToString("yyyy-MM-dd") ?? "")}\",\"{EscapeCsvField(t.Priority)}\",\"{EscapeCsvField(t.Notes)}\"")
                );
                File.WriteAllLines(_csvFilePath, lines, Encoding.UTF8);
            }
            catch (IOException ex)
            {
                StatusText.Text = $"Error saving tasks: {ex.Message}";
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
        private int _taskID;
        private string _taskName;
        private string _status;
        private string _assignedUser;
        private DateTime? _dueDate;
        private string _notes;
        private string _priority;

        // Unique ID for the task
        public int TaskID
        {
            get => _taskID;
            set
            {
                _taskID = value;
                OnPropertyChanged(nameof(TaskID));
            }
        }

        // Name or description of the task
        public string TaskName
        {
            get => _taskName;
            set
            {
                _taskName = value;
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
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(HeatMapColor));
            }
        }

        // User assigned to the task
        public string AssignedUser
        {
            get => _assignedUser;
            set
            {
                _assignedUser = value;
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
}